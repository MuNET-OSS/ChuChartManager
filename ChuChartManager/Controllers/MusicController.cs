using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using ChuChartManager.Models;
using Microsoft.AspNetCore.Mvc;
using MuConvert.chu;

namespace ChuChartManager.Controllers;

using ChuUgcParser = MuConvert.chu.UgcParser;

[ApiController]
[Route("api/[controller]/[action]")]
public class MusicController(MusicScannerService scannerService) : ControllerBase
{
    private static readonly ConcurrentDictionary<string, byte[]> JacketCache = new();

    [HttpGet]
    public ActionResult<List<MusicListItem>> GetMusicList([FromQuery] string? source = null)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return Ok(new List<MusicListItem>());

        IEnumerable<MusicXml> musics;
        if (!string.IsNullOrEmpty(source))
        {
            if (scanner.MusicBySource.TryGetValue(source, out var list))
                musics = list;
            else
                return Ok(new List<MusicListItem>());
        }
        else
        {
            musics = scanner.MusicBySource.Values.SelectMany(l => l);
        }

        var result = musics.OrderBy(m => m.Id).Select(m => new MusicListItem
        {
            Id = m.Id,
            Name = m.Name,
            Artist = m.Artist,
            GenreId = m.GenreId,
            Genres = m.Genres,
            AssetDir = m.AssetDir,
            HasJacket = m.GetJacketFullPath() != null,
            WorldsEndTag = m.WorldsEndTag,
            IsWorldsEnd = m.IsWorldsEnd,
            Fumens = m.Fumens.Select((f, i) => f == null ? null : new FumenSummary
            {
                Index = i,
                Enable = f.Enable,
                Level = f.Level,
                LevelDecimal = f.LevelDecimal,
                LevelDisplay = f.LevelDisplay,
                NotesDesigner = f.NotesDesigner
            }).ToArray()
        }).ToList();

        return Ok(result);
    }

    [HttpGet]
    public ActionResult<List<string>> GetSources()
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return Ok(new List<string>());
        return Ok(scanner.AvailableSources);
    }

    [HttpGet]
    public ActionResult<Dictionary<int, string>> GetGenreMap()
    {
        return Ok(MusicScanner.GenreMap);
    }

    [HttpGet]
    public ActionResult GetJacket([FromQuery] int id, [FromQuery] string assetDir)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        var path = music?.GetJacketFullPath();
        if (path == null) return NotFound();

        if (JacketCache.TryGetValue(path, out var cached))
            return File(cached, "image/png");

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".png")
            return PhysicalFile(path, "image/png");
        if (ext is ".jpg" or ".jpeg")
            return PhysicalFile(path, "image/jpeg");

        try
        {
            var pngData = ConvertDdsToPng(path);
            if (pngData == null) return NotFound();

            JacketCache[path] = pngData;
            return File(pngData, "image/png");
        }
        catch
        {
            return NotFound();
        }
    }

    [HttpPost]
    public ActionResult SaveMusic([FromQuery] int id, [FromQuery] string assetDir, [FromBody] MusicEditDto dto)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound();

        var root = music.XmlDoc.SelectSingleNode("/MusicData");
        if (root == null) return BadRequest();

        var nameNode = root.SelectSingleNode("name/str");
        if (nameNode != null) nameNode.InnerText = dto.Name;

        var artistNode = root.SelectSingleNode("artistName/str");
        if (artistNode != null) artistNode.InnerText = dto.Artist;

        if (dto.GenreId >= 0)
        {
            var genreStrNode = root.SelectSingleNode("genreNames/list/StringID/str");
            var genreIdNode = root.SelectSingleNode("genreNames/list/StringID/id");
            if (genreStrNode != null && dto.GenreName != null) genreStrNode.InnerText = dto.GenreName;
            if (genreIdNode != null) genreIdNode.InnerText = dto.GenreId.ToString();
            music.GenreId = dto.GenreId;
            if (dto.GenreName != null) music.Genres = [dto.GenreName];
        }

        if (dto.Fumens != null)
        {
            var fumenNodes = root.SelectNodes("fumens/MusicFumenData");
            if (fumenNodes != null)
            {
                foreach (var fd in dto.Fumens)
                {
                    if (fd.Index < 0 || fd.Index >= fumenNodes.Count) continue;
                    var node = fumenNodes[fd.Index]!;

                    var enableNode = node.SelectSingleNode("enable");
                    if (enableNode != null) enableNode.InnerText = fd.Enable.ToString().ToLower();

                    var levelNode = node.SelectSingleNode("level");
                    if (levelNode != null) levelNode.InnerText = fd.Level.ToString();

                    var decNode = node.SelectSingleNode("levelDecimal");
                    if (decNode != null) decNode.InnerText = fd.LevelDecimal.ToString();

                    var designerNode = node.SelectSingleNode("notesDesigner");
                    if (designerNode != null) designerNode.InnerText = fd.NotesDesigner;

                    if (music.Fumens[fd.Index] != null)
                    {
                        music.Fumens[fd.Index].Enable = fd.Enable;
                        music.Fumens[fd.Index].Level = fd.Level;
                        music.Fumens[fd.Index].LevelDecimal = fd.LevelDecimal;
                        music.Fumens[fd.Index].NotesDesigner = fd.NotesDesigner;
                    }
                }
            }
        }

        music.Name = dto.Name;
        music.Artist = dto.Artist;
        music.Save();
        return Ok();
    }

    [HttpGet]
    public ActionResult GetAudio([FromQuery] int id, [FromQuery] string assetDir)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound();

        var wav = AudioHelper.GetWavFromMusic(music);
        if (wav == null) return NotFound();

        return File(new MemoryStream(wav), "audio/wav", enableRangeProcessing: true);
    }

    [HttpGet]
    public ActionResult ExportMp3([FromQuery] int id, [FromQuery] string assetDir)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound();

        var wav = AudioHelper.GetWavFromMusic(music);
        if (wav == null) return NotFound();

        using var wavStream = new NAudio.Wave.WaveFileReader(new MemoryStream(wav));
        var mp3Stream = new MemoryStream();
        using (var mp3Writer = new NAudio.Lame.LameMP3FileWriter(mp3Stream, wavStream.WaveFormat, NAudio.Lame.LAMEPreset.STANDARD))
        {
            wavStream.CopyTo(mp3Writer);
        }

        return File(mp3Stream.ToArray(), "audio/mpeg", $"{music.CueFileName}.mp3");
    }

    [HttpPost]
    public ActionResult CopyMusic([FromBody] CopyMusicDto dto)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, dto.Id, dto.AssetDir);
        if (music == null) return NotFound("曲目不存在");

        var sourceDir = music.MusicDirectory;
        if (!Directory.Exists(sourceDir)) return NotFound("源目录不存在");

        var targetOptRoot = ResolveOptRoot(dto.TargetDir);
        if (targetOptRoot == null) return BadRequest("目标目录无效");

        var musicDirName = Path.GetFileName(sourceDir);
        var targetDir = Path.Combine(targetOptRoot, "music", musicDirName);
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
            System.IO.File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);

        var newScanner = new MusicScanner(StaticSettings.GamePath);
        newScanner.ScanAll();
        StaticSettings.Scanner = newScanner;

        return Ok();
    }

    [HttpPost]
    public ActionResult CreateMusic([FromBody] CreateMusicDto dto)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");

        var targetOptRoot = ResolveOptRoot(dto.TargetDir);
        if (targetOptRoot == null) return BadRequest("目标目录无效");

        var musicDirName = $"music{dto.Id:D4}";
        var targetDir = Path.Combine(targetOptRoot, "music", musicDirName);
        if (Directory.Exists(targetDir))
            return BadRequest("该 ID 的曲目目录已存在");

        Directory.CreateDirectory(targetDir);

        var xmlDoc = new System.Xml.XmlDocument();
        xmlDoc.LoadXml($@"<?xml version=""1.0"" encoding=""utf-8""?>
<MusicData xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <dataName>{musicDirName}</dataName>
  <netOpenName><id>2800</id><str>v2_45 00_0</str><data /></netOpenName>
  <releaseTagName><id>0</id><str>v1 1.00.00</str><data /></releaseTagName>
  <disableFlag>false</disableFlag>
  <name><id>{dto.Id}</id><str>{System.Security.SecurityElement.Escape(dto.Name)}</str><data /></name>
  <sortName>{System.Security.SecurityElement.Escape(dto.Name.Length > 0 ? dto.Name[..1] : "")}</sortName>
  <artistName><str>{System.Security.SecurityElement.Escape(dto.Artist)}</str><data /></artistName>
  <genreNames><list><StringID><id>{dto.GenreId}</id><str>{System.Security.SecurityElement.Escape(dto.GenreName)}</str><data /></StringID></list></genreNames>
  <jaketFile><path /></jaketFile>
  <cueFileName><str /></cueFileName>
  <worldsEndTagName><id>-1</id><str /><data /></worldsEndTagName>
  <stageName><str /><data /></stageName>
  <exType>0</exType>
  <enableUltima>false</enableUltima>
  <starDifType>0</starDifType>
  <fumens>
    <MusicFumenData><type><id>0</id><str>BASIC</str><data /></type><enable>false</enable><file><path /></file><level>0</level><levelDecimal>0</levelDecimal><notesDesigner /></MusicFumenData>
    <MusicFumenData><type><id>1</id><str>ADVANCED</str><data /></type><enable>false</enable><file><path /></file><level>0</level><levelDecimal>0</levelDecimal><notesDesigner /></MusicFumenData>
    <MusicFumenData><type><id>2</id><str>EXPERT</str><data /></type><enable>false</enable><file><path /></file><level>0</level><levelDecimal>0</levelDecimal><notesDesigner /></MusicFumenData>
    <MusicFumenData><type><id>3</id><str>MASTER</str><data /></type><enable>false</enable><file><path /></file><level>0</level><levelDecimal>0</levelDecimal><notesDesigner /></MusicFumenData>
    <MusicFumenData><type><id>4</id><str>ULTIMA</str><data /></type><enable>false</enable><file><path /></file><level>0</level><levelDecimal>0</levelDecimal><notesDesigner /></MusicFumenData>
    <MusicFumenData><type><id>5</id><str>WORLD'S END</str><data /></type><enable>false</enable><file><path /></file><level>0</level><levelDecimal>0</levelDecimal><notesDesigner /></MusicFumenData>
  </fumens>
  <priority>0</priority>
</MusicData>");
        xmlDoc.Save(Path.Combine(targetDir, "Music.xml"));

        var newScanner = new MusicScanner(StaticSettings.GamePath);
        newScanner.ScanAll();
        StaticSettings.Scanner = newScanner;

        return Ok();
    }

    [HttpPost]
    public ActionResult ImportJacket([FromQuery] int id, [FromQuery] string assetDir)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound();

        string? selected = null;
        var thread = new Thread(() =>
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "PNG 图片|*.png|所有图片|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "选择封面图片"
            };
            if (dialog.ShowDialog() == DialogResult.OK)
                selected = dialog.FileName;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (selected == null) return Ok(new { imported = false });

        var ddsFileName = $"CHU_UI_Jacket_{id:D8}.dds";
        DdsHelper.ConvertPngToDds(selected, Path.Combine(music.MusicDirectory, ddsFileName));

        var root = music.XmlDoc.SelectSingleNode("/MusicData/jaketFile/path");
        if (root != null) root.InnerText = ddsFileName;
        music.JacketFileName = ddsFileName;
        music.Save();

        JacketCache.TryRemove(music.GetJacketFullPath() ?? "", out _);

        return Ok(new { imported = true });
    }

    [HttpPost]
    public ActionResult ImportChart([FromQuery] int id, [FromQuery] string assetDir, [FromQuery] int diffIndex)
    {

        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound();

        string? selected = null;
        var thread = new Thread(() =>
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "谱面文件|*.c2s;*.ugc;*.sus|C2S|*.c2s|UGC|*.ugc|SUS|*.sus|所有文件|*.*",
                Title = "选择谱面文件"
            };
            if (dialog.ShowDialog() == DialogResult.OK)
                selected = dialog.FileName;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (selected == null) return Ok(new { imported = false, alerts = Array.Empty<string>() });

        var ext = Path.GetExtension(selected).TrimStart('.').ToLowerInvariant();
        var destFileName = $"{id:D4}_0{diffIndex}.c2s";
        var destPath = Path.Combine(music.MusicDirectory, destFileName);
        var alerts = new List<string>();

        if (ext is "ugc" or "sus")
        {
            var sourceContent = System.IO.File.ReadAllText(selected, Encoding.UTF8);
            try
            {
                var (chart, parseAlerts) = ext == "ugc"
                    ? new ChuUgcParser().Parse(sourceContent)
                    : new SusParser().Parse(sourceContent);
                alerts.AddRange(parseAlerts.Select(a => a.ToString()));

                var (c2sContent, genAlerts) = new C2sGenerator().Generate(chart);
                alerts.AddRange(genAlerts.Select(a => a.ToString()));

                System.IO.File.WriteAllText(destPath, c2sContent, Encoding.UTF8);
            }
            catch (MuConvert.utils.ConversionException ex)
            {
                alerts.AddRange(ex.Alerts.Select(a => a.ToString()));
                return BadRequest(new { error = ex.Message, alerts });
            }
        }
        else
        {
            System.IO.File.Copy(selected, destPath, true);
        }

        var fumenNodes = music.XmlDoc.SelectNodes("/MusicData/fumens/MusicFumenData");
        if (fumenNodes != null && diffIndex < fumenNodes.Count)
        {
            var node = fumenNodes[diffIndex]!;
            var enableNode = node.SelectSingleNode("enable");
            if (enableNode != null) enableNode.InnerText = "true";
            var fileNode = node.SelectSingleNode("file/path");
            if (fileNode != null) fileNode.InnerText = destFileName;
        }
        music.Save();

        return Ok(new { imported = true, convertedFrom = ext != "c2s" ? ext : (string?)null, alerts });
    }

    [HttpGet]
    public ActionResult ExportChart([FromQuery] int id, [FromQuery] string assetDir, [FromQuery] int diffIndex, [FromQuery] string format = "ugc")
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound();

        var fumenNodes = music.XmlDoc.SelectNodes("/MusicData/fumens/MusicFumenData");
        if (fumenNodes == null || diffIndex >= fumenNodes.Count) return NotFound();
        var fileNode = fumenNodes[diffIndex]!.SelectSingleNode("file/path");
        if (fileNode == null || string.IsNullOrEmpty(fileNode.InnerText)) return NotFound();

        var c2sPath = Path.Combine(music.MusicDirectory, fileNode.InnerText);
        if (!System.IO.File.Exists(c2sPath)) return NotFound();

        var c2sContent = System.IO.File.ReadAllText(c2sPath, Encoding.UTF8);
        var targetFormat = format.ToLowerInvariant();

        if (targetFormat == "c2s")
            return File(Encoding.UTF8.GetBytes(c2sContent), "application/octet-stream", Path.GetFileName(c2sPath));

        try
        {
            var (chart, _) = new C2sParser().Parse(c2sContent);
            var (output, _) = targetFormat switch
            {
                "ugc" => new UgcGenerator().Generate(chart),
                "sus" => new SusGenerator().Generate(chart),
                _ => throw new ArgumentException($"不支持的目标格式: {format}"),
            };

            var outputName = Path.GetFileNameWithoutExtension(fileNode.InnerText) + $".{targetFormat}";
            return File(Encoding.UTF8.GetBytes(output), "application/octet-stream", outputName);
        }
        catch (MuConvert.utils.ConversionException ex)
        {
            return BadRequest(new { error = ex.Message, alerts = ex.Alerts.Select(a => a.ToString()).ToList() });
        }
    }

    [HttpPost]
    public ActionResult BatchSetProps([FromBody] BatchSetPropsDto dto)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        foreach (var item in dto.Ids)
        {
            var music = FindMusic(scanner, item.Id, item.AssetDir);
            if (music == null) continue;

            var root = music.XmlDoc.SelectSingleNode("/MusicData");
            if (root == null) continue;

            if (dto.GenreId >= 0)
            {
                var genreIdNode = root.SelectSingleNode("genreNames/list/StringID/id");
                var genreStrNode = root.SelectSingleNode("genreNames/list/StringID/str");
                if (genreIdNode != null) genreIdNode.InnerText = dto.GenreId.ToString();
                if (genreStrNode != null && dto.GenreName != null) genreStrNode.InnerText = dto.GenreName;
                music.GenreId = dto.GenreId;
            }

            if (dto.Fumens != null)
            {
                var fumenNodes = root.SelectNodes("fumens/MusicFumenData");
                if (fumenNodes != null)
                {
                    foreach (var fd in dto.Fumens)
                    {
                        if (fd.Index < 0 || fd.Index >= fumenNodes.Count) continue;
                        var node = fumenNodes[fd.Index]!;
                        var designerNode = node.SelectSingleNode("notesDesigner");
                        if (designerNode != null && fd.NotesDesigner != null)
                            designerNode.InnerText = fd.NotesDesigner;
                    }
                }
            }

            music.Save();
        }

        return Ok();
    }

    [HttpPost]
    public ActionResult BatchExportJackets([FromBody] BatchMusicIdDto dto)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var zipStream = HttpContext.Response.BodyWriter.AsStream();
        HttpContext.Response.ContentType = "application/zip";
        HttpContext.Response.Headers["Content-Disposition"] = "attachment; filename=\"jackets.zip\"";

        using var zipArchive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true);
        foreach (var item in dto.Ids)
        {
            var music = FindMusic(scanner, item.Id, item.AssetDir);
            var path = music?.GetJacketFullPath();
            if (path == null) continue;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".dds")
            {
                var pngData = ConvertDdsToPng(path);
                if (pngData == null) continue;
                var entry = zipArchive.CreateEntry($"{music!.Id:D4}_{music.Name}.png");
                using var stream = entry.Open();
                stream.Write(pngData);
            }
            else
            {
                zipArchive.CreateEntryFromFile(path, $"{music!.Id:D4}_{music.Name}{ext}");
            }
        }

        return new EmptyResult();
    }

    [HttpPost]
    public ActionResult BatchExportMp3([FromBody] BatchMusicIdDto dto)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var zipStream = HttpContext.Response.BodyWriter.AsStream();
        HttpContext.Response.ContentType = "application/zip";
        HttpContext.Response.Headers["Content-Disposition"] = "attachment; filename=\"audio.zip\"";

        using var zipArchive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true);
        foreach (var item in dto.Ids)
        {
            var music = FindMusic(scanner, item.Id, item.AssetDir);
            if (music == null) continue;

            var wav = AudioHelper.GetWavFromMusic(music);
            if (wav == null) continue;

            using var wavStream = new NAudio.Wave.WaveFileReader(new MemoryStream(wav));
            var mp3Stream = new MemoryStream();
            using (var mp3Writer = new NAudio.Lame.LameMP3FileWriter(mp3Stream, wavStream.WaveFormat, NAudio.Lame.LAMEPreset.STANDARD))
                wavStream.CopyTo(mp3Writer);

            var entry = zipArchive.CreateEntry($"{music.Id:D4}_{music.Name}.mp3");
            using var entryStream = entry.Open();
            mp3Stream.Position = 0;
            mp3Stream.CopyTo(entryStream);
        }

        return new EmptyResult();
    }

    private static string? ResolveOptRoot(string dirName)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath) || string.IsNullOrWhiteSpace(dirName))
            return null;

        var dataPath = Path.Combine(StaticSettings.GamePath, "data", dirName);
        if (Directory.Exists(dataPath)) return dataPath;

        var optionPath = Path.Combine(StaticSettings.GamePath, "bin", "option", dirName);
        if (Directory.Exists(optionPath)) return optionPath;

        return null;
    }

    private static MusicXml? FindMusic(MusicScanner scanner, int id, string assetDir)
    {
        if (!scanner.MusicBySource.TryGetValue(assetDir, out var list)) return null;
        return list.FirstOrDefault(m => m.Id == id);
    }

    private static readonly object JacketLock = new();

    private static byte[]? ConvertDdsToPng(string ddsPath)
    {
        lock (JacketLock)
        {
            try
            {
                using var image = Pfim.Pfimage.FromFile(ddsPath);
                if (image.Compressed) image.Decompress();

                var pixelFormat = image.Format switch
                {
                    Pfim.ImageFormat.Rgba32 => PixelFormat.Format32bppArgb,
                    _ => PixelFormat.Format24bppRgb
                };

                var bitmap = new Bitmap(image.Width, image.Height, pixelFormat);
                var bmpData = bitmap.LockBits(new Rectangle(0, 0, image.Width, image.Height),
                    ImageLockMode.WriteOnly, pixelFormat);

                var copyLen = Math.Min(image.Data.Length, Math.Abs(bmpData.Stride) * image.Height);
                Marshal.Copy(image.Data, 0, bmpData.Scan0, copyLen);
                bitmap.UnlockBits(bmpData);

                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                bitmap.Dispose();
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }
    }
}

public class MusicListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Artist { get; set; } = "";
    public int GenreId { get; set; }
    public List<string> Genres { get; set; } = [];
    public string AssetDir { get; set; } = "";
    public bool HasJacket { get; set; }
    public string WorldsEndTag { get; set; } = "";
    public bool IsWorldsEnd { get; set; }
    public FumenSummary?[] Fumens { get; set; } = new FumenSummary?[6];
}

public class FumenSummary
{
    public int Index { get; set; }
    public bool Enable { get; set; }
    public int Level { get; set; }
    public int LevelDecimal { get; set; }
    public string LevelDisplay { get; set; } = "";
    public string NotesDesigner { get; set; } = "";
}

public class MusicEditDto
{
    public string Name { get; set; } = "";
    public string Artist { get; set; } = "";
    public int GenreId { get; set; } = -1;
    public string? GenreName { get; set; }
    public FumenEditDto[]? Fumens { get; set; }
}

public class FumenEditDto
{
    public int Index { get; set; }
    public bool Enable { get; set; }
    public int Level { get; set; }
    public int LevelDecimal { get; set; }
    public string NotesDesigner { get; set; } = "";
}

public class CopyMusicDto
{
    public int Id { get; set; }
    public string AssetDir { get; set; } = "";
    public string TargetDir { get; set; } = "";
}

public class CreateMusicDto
{
    public string TargetDir { get; set; } = "";
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Artist { get; set; } = "";
    public int GenreId { get; set; }
    public string GenreName { get; set; } = "";
}

public class MusicIdAndAssetDir
{
    public int Id { get; set; }
    public string AssetDir { get; set; } = "";
}

public class BatchMusicIdDto
{
    public MusicIdAndAssetDir[] Ids { get; set; } = [];
}

public class BatchSetPropsDto
{
    public MusicIdAndAssetDir[] Ids { get; set; } = [];
    public int GenreId { get; set; } = -1;
    public string? GenreName { get; set; }
    public FumenEditDto[]? Fumens { get; set; }
}
