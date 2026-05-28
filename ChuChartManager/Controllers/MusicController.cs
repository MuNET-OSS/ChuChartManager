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
                NotesDesigner = f.NotesDesigner,
                NoteCount = f.NoteCount
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

        var ddsFileName = $"CHU_UI_Jacket_{id:D4}.dds";
        DdsHelper.ConvertPngToDds(selected, Path.Combine(music.MusicDirectory, ddsFileName));

        var root = music.XmlDoc.SelectSingleNode("/MusicData/jaketFile/path");
        if (root != null) root.InnerText = ddsFileName;
        music.JacketFileName = ddsFileName;
        music.Save();

        JacketCache.TryRemove(music.GetJacketFullPath() ?? "", out _);

        return Ok(new { imported = true });
    }

    [HttpPut]
    public ActionResult SetJacket([FromQuery] int id, [FromQuery] string assetDir, IFormFile file)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound();

        var ddsFileName = $"CHU_UI_Jacket_{id:D4}.dds";
        var tempPath = Path.Combine(Path.GetTempPath(), $"ccm_jacket_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}");
        try
        {
            using (var fs = System.IO.File.Create(tempPath))
                file.CopyTo(fs);
            DdsHelper.ConvertPngToDds(tempPath, Path.Combine(music.MusicDirectory, ddsFileName));
        }
        finally
        {
            try { System.IO.File.Delete(tempPath); } catch { }
        }

        var root = music.XmlDoc.SelectSingleNode("/MusicData/jaketFile/path");
        if (root != null) root.InnerText = ddsFileName;
        music.JacketFileName = ddsFileName;
        music.Save();

        JacketCache.TryRemove(music.GetJacketFullPath() ?? "", out _);

        return Ok();
    }

    [HttpPut]
    [DisableRequestSizeLimit]
    public ActionResult SetAudio([FromQuery] int id, [FromQuery] string assetDir, IFormFile file)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound();

        var tempPath = Path.Combine(Path.GetTempPath(), $"ccm_audio_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}");
        try
        {
            using (var fs = System.IO.File.Create(tempPath))
                file.CopyTo(fs);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext == ".awb")
            {
                var sourceRoot = Path.GetDirectoryName(Path.GetDirectoryName(music.MusicDirectory));
                if (sourceRoot == null) return BadRequest("Cannot determine option root");
                var cueFileDir = Path.Combine(sourceRoot, "cueFile", $"cueFile{id:D6}");
                Directory.CreateDirectory(cueFileDir);
                var awbPath = Path.Combine(cueFileDir, $"music{id:D4}.awb");
                System.IO.File.Copy(tempPath, awbPath, true);
            }
            else
            {
                AudioHelper.ImportAudioToMusic(music, tempPath);
            }
        }
        finally
        {
            try { System.IO.File.Delete(tempPath); } catch { }
        }

        return Ok();
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

    [HttpPut]
    public ActionResult ReplaceChart([FromQuery] int id, [FromQuery] string assetDir, [FromQuery] int diffIndex, IFormFile file)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound();

        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        var destFileName = $"{id:D4}_0{diffIndex}.c2s";
        var destPath = Path.Combine(music.MusicDirectory, destFileName);
        var alerts = new List<string>();

        using var ms = new MemoryStream();
        file.CopyTo(ms);
        var sourceContent = Encoding.UTF8.GetString(ms.ToArray());

        if (ext is "ugc" or "sus")
        {
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
            System.IO.File.WriteAllText(destPath, sourceContent, Encoding.UTF8);
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

    [HttpPost]
    public ActionResult ImportMusicCheck(List<IFormFile> charts)
    {
        if (charts == null || charts.Count == 0)
            return Ok(new { success = false, error = "No chart files provided" });

        var alerts = new List<string>();
        var difficulties = new List<object>();
        string title = "", artist = "";

        foreach (var chart in charts)
        {
            var ext = Path.GetExtension(chart.FileName).TrimStart('.').ToLowerInvariant();
            if (ext is not ("ugc" or "c2s" or "sus")) continue;

            try
            {
                using var reader = new StreamReader(chart.OpenReadStream(), Encoding.UTF8);
                var content = reader.ReadToEnd();

                MuConvert.chu.ChuChart? chartObj = ext switch
                {
                    "ugc" => new ChuUgcParser().Parse(content).Item1,
                    "sus" => new SusParser().Parse(content).Item1,
                    "c2s" => new C2sParser().Parse(content).Item1,
                    _ => null,
                };

                if (chartObj != null)
                {
                    difficulties.Add(new
                    {
                        fileName = chart.FileName,
                        difficulty = chartObj.Difficulty,
                        level = (int)chartObj.Level,
                        levelDecimal = (int)(chartObj.Level % 1 * 100),
                        designer = chartObj.Designer,
                    });
                    if (string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(chartObj.Title))
                        title = chartObj.Title;
                    if (string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(chartObj.Artist))
                        artist = chartObj.Artist;
                }
            }
            catch (MuConvert.utils.ConversionException ex)
            {
                alerts.AddRange(ex.Alerts.Select(a => a.ToString()));
            }
            catch (Exception ex)
            {
                alerts.Add(ex.Message);
            }
        }

        var suggestedId = 8000;
        var scanner = scannerService.Scanner;
        var existingIds = new HashSet<int>();
        if (scanner != null)
        {
            foreach (var list in scanner.MusicBySource.Values)
                foreach (var m in list)
                    existingIds.Add(m.Id);
        }
        while (existingIds.Contains(suggestedId))
            suggestedId++;

        return Ok(new { success = true, alerts, suggestedId, title, artist, difficulties });
    }

    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public ActionResult ImportMusicExecute(
        List<IFormFile> charts,
        IFormFile audio,
        IFormFile? cover,
        [FromForm] int id = 0,
        [FromForm] string? title = "",
        [FromForm] string? artist = "",
        [FromForm] int genreId = 0,
        [FromForm] string? genreName = "",
        [FromForm] string? targetDir = "")
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return Ok(new { success = false, error = "GamePath not set" });

        if (charts == null || charts.Count == 0)
            return Ok(new { success = false, error = "No chart file provided" });
        if (audio == null || audio.Length == 0)
            return Ok(new { success = false, error = "No audio file provided" });

        var optRoot = ResolveOptRoot(targetDir ?? "");
        if (optRoot == null)
            return Ok(new { success = false, error = "Invalid target directory" });

        var musicDirName = $"music{id:D4}";
        var musicDir = Path.Combine(optRoot, "music", musicDirName);
        if (Directory.Exists(musicDir))
            return Ok(new { success = false, error = $"Music directory already exists for ID {id}" });

        var alerts = new List<string>();
        var tempFiles = new List<string>();
        var importedDifficulties = new Dictionary<int, (int level, int levelDecimal)>();

        try
        {
            Directory.CreateDirectory(musicDir);

            // ========== 1. Chart conversion (multi-difficulty) ==========
            foreach (var chart in charts)
            {
                var chartExt = Path.GetExtension(chart.FileName).TrimStart('.').ToLowerInvariant();
                if (chartExt is not ("ugc" or "c2s" or "sus")) continue;

                using var chartReader = new StreamReader(chart.OpenReadStream(), Encoding.UTF8);
                var chartContent = chartReader.ReadToEnd();

                MuConvert.chu.ChuChart? chartObj = null;
                string c2sContent;

                if (chartExt is "ugc" or "sus")
                {
                    var (parsed, parseAlerts) = chartExt == "ugc"
                        ? new ChuUgcParser().Parse(chartContent)
                        : new SusParser().Parse(chartContent);
                    chartObj = parsed;
                    alerts.AddRange(parseAlerts.Select(a => a.ToString()));

                    var (generated, genAlerts) = new C2sGenerator().Generate(chartObj);
                    c2sContent = generated;
                    alerts.AddRange(genAlerts.Select(a => a.ToString()));
                }
                else
                {
                    var (parsed, _) = new C2sParser().Parse(chartContent);
                    chartObj = parsed;
                    c2sContent = chartContent;
                }

                var difficulty = chartObj?.Difficulty ?? 3;
                var level = (int)(chartObj?.Level ?? 0);
                var levelDecimal = (int)((chartObj?.Level ?? 0) % 1 * 100);

                var chartFileName = $"{id:D4}_0{difficulty}.c2s";
                var chartDestPath = Path.Combine(musicDir, chartFileName);
                c2sContent = FixC2sHeader(c2sContent, id);
                System.IO.File.WriteAllText(chartDestPath, c2sContent, Encoding.UTF8);

                importedDifficulties[difficulty] = (level, levelDecimal);
            }

            // ========== 2. Audio conversion ==========
            var audioExt = Path.GetExtension(audio.FileName).TrimStart('.').ToLowerInvariant();
            byte[] wavBytes;

            using (var audioStream = audio.OpenReadStream())
            using (var ms = new MemoryStream())
            {
                audioStream.CopyTo(ms);
                var audioBytes = ms.ToArray();

                if (audioExt == "wav")
                {
                    wavBytes = audioBytes;
                }
                else if (audioExt == "mp3")
                {
                    // Decode MP3 to WAV (NAudio Mp3FileReader requires Stream)
                    using var mp3Stream = new MemoryStream(audioBytes);
                    using var mp3Reader = new NAudio.Wave.Mp3FileReader(mp3Stream);
                    using var wavMs = new MemoryStream();
                    var sampleProvider = NAudio.Wave.WaveExtensionMethods.ToSampleProvider(mp3Reader);
                    var pcm16 = NAudio.Wave.WaveExtensionMethods.ToWaveProvider16(sampleProvider);
                    NAudio.Wave.WaveFileWriter.WriteWavFileToStream(wavMs, pcm16);
                    wavBytes = wavMs.ToArray();
                }
                else
                {
                    return Ok(new { success = false, error = $"Unsupported audio format: {audioExt}" });
                }
            }

            var hcaBytes = AudioHelper.EncodeWavToHca(wavBytes);
            if (hcaBytes == null || hcaBytes.Length == 0)
                return Ok(new { success = false, error = "Failed to encode WAV to HCA" });

            var cueFileName = $"music{id:D4}";
            var cueFileDir = Path.Combine(optRoot, "cueFile", $"cueFile{id:D6}");
            Directory.CreateDirectory(cueFileDir);

            AudioHelper.RepackAcbWithHca(cueFileDir, cueFileName, hcaBytes);

            // Generate CueFile.xml
            var cueFileXmlPath = Path.Combine(cueFileDir, "CueFile.xml");
            var cueFileXmlDoc = new System.Xml.XmlDocument();
            cueFileXmlDoc.LoadXml($@"<?xml version=""1.0"" encoding=""utf-8""?>
<CueFileData>
  <dataName>cueFile{id:D6}</dataName>
  <name><id>{id}</id><str>{cueFileName}</str><data /></name>
  <acbFile><path>{cueFileName}.acb</path></acbFile>
  <awbFile><path>{cueFileName}.awb</path></awbFile>
</CueFileData>");
            cueFileXmlDoc.Save(cueFileXmlPath);

            // ========== 3. Cover (jacket) ==========
            string? jacketFileName = null;
            if (cover != null && cover.Length > 0)
            {
                var coverExt = Path.GetExtension(cover.FileName).ToLowerInvariant();
                if (coverExt is not (".png" or ".jpg" or ".jpeg"))
                {
                    alerts.Add($"Unsupported cover format '{coverExt}', skipping");
                }
                else
                {
                    var tmpCoverPath = Path.Combine(Path.GetTempPath(), $"chuchart_{id}_{Guid.NewGuid():N}{coverExt}");
                    tempFiles.Add(tmpCoverPath);
                    using (var coverFs = System.IO.File.Create(tmpCoverPath))
                        cover.CopyTo(coverFs);

                    jacketFileName = $"CHU_UI_Jacket_{id:D4}.dds";
                    DdsHelper.ConvertPngToDds(tmpCoverPath, Path.Combine(musicDir, jacketFileName));
                }
            }

            // ========== 4. Generate Music.xml ==========
            var difficultyStrs = new[] { "Basic", "Advanced", "Expert", "Master", "Ultima" };
            var difficultyData = new[] { "BASIC", "ADVANCED", "EXPERT", "MASTER", "ULTIMA" };
            var fumenLines = new StringBuilder();
            for (var i = 0; i < 5; i++)
            {
                var hasChart = importedDifficulties.ContainsKey(i);
                var enable = hasChart ? "true" : "false";
                var lvl = hasChart ? importedDifficulties[i].level : 0;
                var lvlDec = hasChart ? importedDifficulties[i].levelDecimal : 0;
                var filePathXml = hasChart ? $"<path>{id:D4}_0{i}.c2s</path>" : "<path />";
                fumenLines.AppendLine(
                    $"    <MusicFumenData><type><id>{i}</id><str>{difficultyStrs[i]}</str><data>{difficultyData[i]}</data></type>" +
                    $"<enable>{enable}</enable><file>{filePathXml}</file>" +
                    $"<level>{lvl}</level><levelDecimal>{lvlDec}</levelDecimal><notesDesigner /><defaultBpm>0</defaultBpm></MusicFumenData>");
            }

            var enableUltima = importedDifficulties.ContainsKey(4) ? "true" : "false";
            var jaketFileXml = jacketFileName != null
                ? $"<path>{jacketFileName}</path>"
                : "<path />";

            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.LoadXml($@"<?xml version=""1.0"" encoding=""utf-8""?>
<MusicData>
  <dataName>{musicDirName}</dataName>
  <releaseTagName><id>0</id><str>v1 1.00.00</str><data /></releaseTagName>
  <netOpenName><id>2800</id><str>v2_45 00_0</str><data /></netOpenName>
  <disableFlag>false</disableFlag>
  <exType>0</exType>
  <name><id>{id}</id><str>{System.Security.SecurityElement.Escape(title ?? "")}</str><data /></name>
  <sortName>{System.Security.SecurityElement.Escape((title ?? "").Length > 0 ? (title ?? "")[..1].ToUpperInvariant() : "")}</sortName>
  <artistName><id>{id}</id><str>{System.Security.SecurityElement.Escape(artist ?? "")}</str><data /></artistName>
  <genreNames><list><StringID><id>{genreId}</id><str>{System.Security.SecurityElement.Escape(genreName ?? "")}</str><data /></StringID></list></genreNames>
  <worksName><id>-1</id><str>Invalid</str><data /></worksName>
  <labelName><id>-1</id><str>Invalid</str><data /></labelName>
  <jaketFile>{jaketFileXml}</jaketFile>
  <firstLock>false</firstLock>
  <enableUltima>{enableUltima}</enableUltima>
  <isGiftMusic>false</isGiftMusic>
  <releaseDate>20240101</releaseDate>
  <priority>0</priority>
  <cueFileName><id>{id}</id><str>{cueFileName}</str><data /></cueFileName>
  <worldsEndTagName><id>-1</id><str>Invalid</str><data /></worldsEndTagName>
  <starDifType>0</starDifType>
  <stageName><id>-1</id><str>Invalid</str><data /></stageName>
  <fumens>
{fumenLines}    <MusicFumenData><type><id>5</id><str>WorldsEnd</str><data>WORLD'S END</data></type><enable>false</enable><file><path /></file><level>0</level><levelDecimal>0</levelDecimal><notesDesigner /><defaultBpm>0</defaultBpm></MusicFumenData>
  </fumens>
</MusicData>");
            xmlDoc.Save(Path.Combine(musicDir, "Music.xml"));

            // ========== 5. Rescan ==========
            var newScanner = new MusicScanner(StaticSettings.GamePath);
            newScanner.ScanAll();
            StaticSettings.Scanner = newScanner;

            return Ok(new { success = true, alerts });
        }
        catch (MuConvert.utils.ConversionException ex)
        {
            alerts.AddRange(ex.Alerts.Select(a => a.ToString()));
            return BadRequest(new { success = false, error = ex.Message, alerts });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message, alerts });
        }
        finally
        {
            foreach (var tmp in tempFiles)
            {
                try { if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp); }
                catch { /* ignore cleanup failures */ }
            }
        }
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

    private static string FixC2sHeader(string c2s, int musicId)
    {
        var lines = c2s.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("VERSION\t"))
                lines[i] = "VERSION\t1.13.00\t1.13.00";
            else if (lines[i].StartsWith("MUSIC\t"))
                lines[i] = "MUSIC\t0";
            else if (lines[i].StartsWith("SEQUENCEID\t"))
                lines[i] = "SEQUENCEID\t0";
            else if (lines[i].StartsWith("DIFFICULT\t"))
                lines[i] = "DIFFICULT\t00";
        }
        return string.Join("\r\n", lines);
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

    [HttpGet]
    public ActionResult ExportOpt([FromQuery] int id, [FromQuery] string assetDir)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound();

        var musicDir = music.MusicDirectory;
        if (!Directory.Exists(musicDir)) return NotFound();

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var musicDirName = Path.GetFileName(musicDir);
            foreach (var file in Directory.GetFiles(musicDir))
                zip.CreateEntryFromFile(file, $"music/{musicDirName}/{Path.GetFileName(file)}");

            var awbPath = AudioHelper.FindAwbPath(music);
            if (awbPath != null)
            {
                var cueDir = Path.GetDirectoryName(awbPath)!;
                var cueDirName = Path.GetFileName(cueDir);
                foreach (var file in Directory.GetFiles(cueDir))
                    zip.CreateEntryFromFile(file, $"cueFile/{cueDirName}/{Path.GetFileName(file)}");
            }
        }

        ms.Seek(0, SeekOrigin.Begin);
        return File(ms, "application/zip", $"{id:D4} - {music.Name}.zip");
    }

    [HttpGet]
    public ActionResult ExportCustom([FromQuery] int id, [FromQuery] string assetDir, [FromQuery] string format = "ugc")
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound();

        var safeName = string.Join("_", music.Name.Split(Path.GetInvalidFileNameChars())).TrimEnd('.', ' ');
        var ext = format.ToLowerInvariant() == "sus" ? "sus" : "ugc";
        var diffFileNames = new[] { "bas", "adv", "exp", "mas", "ult", "we" };

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            for (var i = 0; i < music.Fumens.Length; i++)
            {
                var fumen = music.Fumens[i];
                if (fumen is not { Enable: true } || string.IsNullOrEmpty(fumen.FilePath)) continue;

                var c2sPath = Path.Combine(music.MusicDirectory, fumen.FilePath);
                if (!System.IO.File.Exists(c2sPath)) continue;

                var c2sContent = System.IO.File.ReadAllText(c2sPath, Encoding.UTF8);
                try
                {
                    var (chart, _) = new C2sParser().Parse(c2sContent);
                    chart.Difficulty = i;
                    chart.Title = music.Name;
                    chart.Artist = music.Artist;
                    chart.Designer = fumen.NotesDesigner;
                    chart.DisplayLevel = fumen.LevelDisplay;
                    chart.Level = fumen.LevelValue;
                    chart.MusicId = music.Id.ToString();
                    var content = ext == "sus"
                        ? new SusGenerator().Generate(chart).Item1
                        : new UgcGenerator().Generate(chart).Item1;

                    content = $"' Converted with MuConvert by ChuChartManager\r\n{content}";

                    if (ext == "ugc")
                    {
                        var ugcMeta = "@BGM\tbgm.wav\r\n@BGMOFS\t0.00000\r\n@BGMPRV\t0.00000\t0.00000\r\n@JACKET\tjacket.png\r\n";
                        content = content.Replace("@SONGID\t", ugcMeta + "@SONGID\t");
                        if (!content.Contains("@EXVER"))
                            content = content.Replace("@VER\t8", "@VER\t8\r\n@EXVER\t1");
                    }

                    var fileName = i < diffFileNames.Length ? diffFileNames[i] : $"diff{i}";
                    var entry = zip.CreateEntry($"{safeName}/{fileName}.{ext}");
                    using var w = new StreamWriter(entry.Open(), Encoding.UTF8);
                    w.Write(content);
                }
                catch
                {
                    var fileName = i < diffFileNames.Length ? diffFileNames[i] : $"diff{i}";
                    zip.CreateEntryFromFile(c2sPath, $"{safeName}/{fileName}.c2s");
                }
            }

            var wav = AudioHelper.GetWavFromMusic(music);
            if (wav != null)
            {
                var entry = zip.CreateEntry($"{safeName}/bgm.wav");
                using var s = entry.Open();
                s.Write(wav);
            }

            var jacketPath = music.GetJacketFullPath();
            if (jacketPath != null)
            {
                var pngData = ConvertDdsToPng(jacketPath);
                if (pngData != null)
                {
                    var entry = zip.CreateEntry($"{safeName}/jacket.png");
                    using var s = entry.Open();
                    s.Write(pngData);
                }
                else if (Path.GetExtension(jacketPath).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg")
                {
                    zip.CreateEntryFromFile(jacketPath, $"{safeName}/jacket{Path.GetExtension(jacketPath)}");
                }
            }
        }

        ms.Seek(0, SeekOrigin.Begin);
        return File(ms, "application/zip", $"{safeName}.zip");
    }

    [HttpPost]
    public ActionResult ChangeId([FromQuery] int id, [FromQuery] string assetDir, [FromBody] int newId)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound("曲目不存在");

        if (assetDir == "A000") return BadRequest("不能修改 A000 的曲目 ID");

        var optRoot = ResolveOptRoot(assetDir);
        if (optRoot == null) return BadRequest("目录无效");

        var newMusicDirName = $"music{newId:D4}";
        var newMusicDir = Path.Combine(optRoot, "music", newMusicDirName);
        if (Directory.Exists(newMusicDir) && newId != id)
            return BadRequest($"ID {newId} 的曲目目录已存在");

        var oldMusicDir = music.MusicDirectory;

        // Music.xml: 更新 ID
        var root = music.XmlDoc.SelectSingleNode("/MusicData");
        var idNode = root?.SelectSingleNode("name/id");
        if (idNode != null) idNode.InnerText = newId.ToString();
        var dataNameNode = root?.SelectSingleNode("dataName");
        if (dataNameNode != null) dataNameNode.InnerText = newMusicDirName;
        music.Save();

        // 重命名 music 目录
        if (newId != id && oldMusicDir != newMusicDir)
            Directory.Move(oldMusicDir, newMusicDir);

        // 重命名 cueFile 目录
        var oldCueDir = Path.Combine(optRoot, "cueFile", $"cueFile{id:D6}");
        var newCueDir = Path.Combine(optRoot, "cueFile", $"cueFile{newId:D6}");
        if (newId != id && Directory.Exists(oldCueDir) && !Directory.Exists(newCueDir))
            Directory.Move(oldCueDir, newCueDir);

        // 重新扫描
        var newScanner = new MusicScanner(StaticSettings.GamePath);
        newScanner.ScanAll();
        StaticSettings.Scanner = newScanner;

        return Ok();
    }

    [HttpPost]
    public ActionResult DeleteMusic([FromQuery] int id, [FromQuery] string assetDir)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound("曲目不存在");

        if (assetDir == "A000") return BadRequest("不能删除 A000 的曲目");

        if (Directory.Exists(music.MusicDirectory))
            Directory.Delete(music.MusicDirectory, true);

        var optRoot = ResolveOptRoot(assetDir);
        if (optRoot != null)
        {
            var cueDir = Path.Combine(optRoot, "cueFile", $"cueFile{id:D6}");
            if (Directory.Exists(cueDir))
                Directory.Delete(cueDir, true);
        }

        var newScanner = new MusicScanner(StaticSettings.GamePath);
        newScanner.ScanAll();
        StaticSettings.Scanner = newScanner;

        return Ok();
    }

    [HttpPost]
    public ActionResult OpenExplorer([FromQuery] int id, [FromQuery] string assetDir)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound();

        if (Directory.Exists(music.MusicDirectory))
            System.Diagnostics.Process.Start("explorer.exe", music.MusicDirectory);

        return Ok();
    }

    [HttpPost]
    public ActionResult OpenXml([FromQuery] int id, [FromQuery] string assetDir)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null) return NotFound();

        var music = FindMusic(scanner, id, assetDir);
        if (music == null) return NotFound();

        var xmlPath = Path.Combine(music.MusicDirectory, "Music.xml");
        if (System.IO.File.Exists(xmlPath))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(xmlPath) { UseShellExecute = true });

        return Ok();
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
    public int NoteCount { get; set; }
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

