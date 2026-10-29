using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class CustomResourceController : ControllerBase
{
    private static readonly (string type, string xmlName, string rootNode, string dirPrefix, int dirPadding)[] ResourceTypes =
    [
        ("trophy", "Trophy.xml", "TrophyData", "trophy", 6),
        ("namePlate", "NamePlate.xml", "NamePlateData", "namePlate", 8),
        ("frame", "Frame.xml", "FrameData", "frame", 8),
        ("mapIcon", "MapIcon.xml", "MapIconData", "mapIcon", 4),
        ("avatarAccessory", "AvatarAccessory.xml", "AvatarAccessoryData", "avatarAccessory", 8),
        ("chara", "Chara.xml", "CharaData", "chara", 6),
        ("systemVoice", "SystemVoice.xml", "SystemVoiceData", "systemVoice", 4),
    ];

    #region List / Delete / Preview

    public class ResourceListItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string AssetDir { get; set; } = "";
        public string DirPath { get; set; } = "";
        public bool HasImage { get; set; }
        public int RareType { get; set; }
    }

    [HttpGet]
    public ActionResult<List<ResourceListItem>> GetResourceList([FromQuery] string type, [FromQuery] string? source = null)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return Ok(new List<ResourceListItem>());

        var typeDef = ResourceTypes.FirstOrDefault(t => t.type == type);
        if (typeDef == default) return BadRequest("Invalid resource type");

        var result = new List<ResourceListItem>();
        foreach (var (dir, assetDir) in EnumerateResourceDirs(typeDef.type, source))
        {
            var xmlPath = Path.Combine(dir, typeDef.xmlName);
            if (!System.IO.File.Exists(xmlPath)) continue;

            try
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                var root = doc.SelectSingleNode($"/{typeDef.rootNode}");
                if (root == null) continue;

                var id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var rid) ? rid : 0;
                var name = root.SelectSingleNode("name/str")?.InnerText ?? "";
                var hasImage = Directory.EnumerateFiles(dir, "*.dds").Any();
                var rareType = int.TryParse(root.SelectSingleNode("rareType")?.InnerText, out var rt) ? rt : 0;

                if (!hasImage && type == "chara")
                {
                    var parentRoot = Path.GetDirectoryName(Path.GetDirectoryName(dir));
                    if (parentRoot != null)
                    {
                        var ddsImageDir = Path.Combine(parentRoot, "ddsImage", $"ddsImage{id:D6}");
                        hasImage = Directory.Exists(ddsImageDir) && Directory.EnumerateFiles(ddsImageDir, "*.dds").Any();
                    }
                }

                result.Add(new ResourceListItem
                {
                    Id = id,
                    Name = name,
                    Type = type,
                    AssetDir = assetDir,
                    DirPath = dir,
                    HasImage = hasImage,
                    RareType = rareType,
                });
            }
            catch { }
        }

        return Ok(result.OrderBy(r => r.Id).ToList());
    }

    [HttpPost]
    public ActionResult DeleteResource([FromQuery] string type, [FromQuery] int id, [FromQuery] string assetDir)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");

        var typeDef = ResourceTypes.FirstOrDefault(t => t.type == type);
        if (typeDef == default) return BadRequest("Invalid resource type");

        var dir = FindResourceDir(typeDef, id, assetDir);
        if (dir == null) return NotFound();

        Directory.Delete(dir, true);

        if (type == "chara")
        {
            var ddsImageDirName = $"ddsImage{id:D6}";
            var ddsImageRoot = Path.GetDirectoryName(Path.GetDirectoryName(dir));
            if (ddsImageRoot != null)
            {
                var ddsImageDir = Path.Combine(ddsImageRoot, "ddsImage", ddsImageDirName);
                if (Directory.Exists(ddsImageDir))
                    Directory.Delete(ddsImageDir, true);
            }
        }

        return Ok();
    }

    [HttpPost]
    public ActionResult DeleteMusic([FromQuery] int id, [FromQuery] string assetDir)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");

        string musicRoot;
        if (assetDir == "A000")
            musicRoot = Path.Combine(StaticSettings.GamePath, "data", "A000", "music");
        else
        {
            var optionPath = OptionPathResolver.ResolveExisting(StaticSettings.GamePath, assetDir);
            if (optionPath == null) return NotFound();
            musicRoot = Path.Combine(optionPath, "music");
        }

        if (!Directory.Exists(musicRoot)) return NotFound();

        foreach (var dir in Directory.EnumerateDirectories(musicRoot))
        {
            var xmlPath = Path.Combine(dir, "Music.xml");
            if (!System.IO.File.Exists(xmlPath)) continue;
            try
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                var mid = doc.SelectSingleNode("/MusicData/name/id")?.InnerText;
                if (mid != null && int.TryParse(mid, out var parsedId) && parsedId == id)
                {
                    Directory.Delete(dir, true);
                    return Ok();
                }
            }
            catch { }
        }

        return NotFound();
    }

    private static readonly object PreviewLock = new();

    [HttpGet]
    public ActionResult GetResourcePreview([FromQuery] string type, [FromQuery] int id, [FromQuery] string assetDir)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return NotFound();

        var typeDef = ResourceTypes.FirstOrDefault(t => t.type == type);
        if (typeDef == default) return BadRequest("Invalid resource type");

        var dir = FindResourceDir(typeDef, id, assetDir);
        if (dir == null) return NotFound();

        var ddsFile = Directory.EnumerateFiles(dir, "*.dds").FirstOrDefault();
        if (ddsFile == null && type == "chara")
        {
            var ddsImageRoot = Path.GetDirectoryName(Path.GetDirectoryName(dir));
            if (ddsImageRoot != null)
            {
                var ddsImageDir = Path.Combine(ddsImageRoot, "ddsImage", $"ddsImage{id:D6}");
                if (Directory.Exists(ddsImageDir))
                    ddsFile = Directory.EnumerateFiles(ddsImageDir, "*.dds").FirstOrDefault();
            }
        }

        if (ddsFile == null) return NotFound();

        var pngData = ConvertDdsToPng(ddsFile);
        if (pngData == null) return NotFound();

        return File(pngData, "image/png");
    }

    private static byte[]? ConvertDdsToPng(string ddsPath)
    {
        lock (PreviewLock)
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
                var bmpData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
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

    private static IEnumerable<(string dir, string assetDir)> EnumerateResourceDirs(string type, string? source)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath)) yield break;

        if (source == null || source == "A000")
        {
            var baseDir = Path.Combine(StaticSettings.GamePath, "data", "A000", type);
            if (Directory.Exists(baseDir))
                foreach (var d in Directory.EnumerateDirectories(baseDir))
                    yield return (d, "A000");
        }

        foreach (var (dirName, optDir) in OptionPathResolver.EnumerateOptionDirectories(StaticSettings.GamePath))
        {
            if (source != null && source != dirName) continue;

            var resDir = Path.Combine(optDir, type);
            if (!Directory.Exists(resDir)) continue;

            foreach (var d in Directory.EnumerateDirectories(resDir))
                yield return (d, dirName);
        }
    }

    private static string? FindResourceDir(
        (string type, string xmlName, string rootNode, string dirPrefix, int dirPadding) typeDef,
        int id, string assetDir)
    {
        string resRoot;
        if (assetDir == "A000")
            resRoot = Path.Combine(StaticSettings.GamePath, "data", "A000", typeDef.type);
        else
        {
            var optionPath = OptionPathResolver.ResolveExisting(StaticSettings.GamePath, assetDir);
            if (optionPath == null) return null;
            resRoot = Path.Combine(optionPath, typeDef.type);
        }

        if (!Directory.Exists(resRoot)) return null;

        foreach (var dir in Directory.EnumerateDirectories(resRoot))
        {
            var xmlPath = Path.Combine(dir, typeDef.xmlName);
            if (!System.IO.File.Exists(xmlPath)) continue;
            try
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                var resId = doc.SelectSingleNode($"/{typeDef.rootNode}/name/id")?.InnerText;
                if (resId != null && int.TryParse(resId, out var rid) && rid == id)
                    return dir;
            }
            catch { }
        }

        return null;
    }

    #endregion

    #region System Voice Audio

    /// <summary>获取系统语音的 cue 列表（AWB 中的条目数量）</summary>
    [HttpGet]
    public ActionResult GetSystemVoiceCueList([FromQuery] int id, [FromQuery] string assetDir)
    {
        var awbPath = FindSystemVoiceAwb(id, assetDir);
        if (awbPath == null) return NotFound();

        try
        {
            var archive = new SonicAudioLib.Archives.CriAfs2Archive();
            using var fs = System.IO.File.OpenRead(awbPath);
            archive.Read(fs);
            var cueCount = archive.Count();
            return Ok(new { cueCount, id, assetDir });
        }
        catch
        {
            return StatusCode(500, "Failed to read AWB");
        }
    }

    /// <summary>获取系统语音指定 cue 的音频（WAV 格式）</summary>
    [HttpGet]
    public ActionResult GetSystemVoiceAudio([FromQuery] int id, [FromQuery] string assetDir, [FromQuery] int cueIndex = 0)
    {
        var awbPath = FindSystemVoiceAwb(id, assetDir);
        if (awbPath == null) return NotFound();

        try
        {
            var archive = new SonicAudioLib.Archives.CriAfs2Archive();
            using var fs = System.IO.File.OpenRead(awbPath);
            archive.Read(fs);

            var entry = archive.ElementAtOrDefault(cueIndex);
            if (entry == null) return NotFound("Cue index out of range");

            var hcaData = new byte[entry.Length];
            fs.Seek(entry.Position, SeekOrigin.Begin);
            fs.ReadExactly(hcaData);

            var wavData = AudioHelper.DecodeHcaToWav(hcaData);
            if (wavData == null) return StatusCode(500, "Failed to decode HCA");

            return File(wavData, "audio/wav", $"systemvoice{id:D4}_cue{cueIndex}.wav");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to extract audio: {ex.Message}");
        }
    }

    private string? FindSystemVoiceAwb(int systemVoiceId, string assetDir)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath)) return null;

        // 先找 SystemVoice XML 获取 cue id
        var svTypeDef = ResourceTypes.First(t => t.type == "systemVoice");
        var svDir = FindResourceDir(svTypeDef, systemVoiceId, assetDir);
        if (svDir == null) return null;

        var xmlPath = Path.Combine(svDir, "SystemVoice.xml");
        if (!System.IO.File.Exists(xmlPath)) return null;

        var doc = new XmlDocument();
        doc.Load(xmlPath);
        var cueStr = doc.SelectSingleNode("/SystemVoiceData/cue/str")?.InnerText;
        if (string.IsNullOrEmpty(cueStr)) return null;

        // 在 cueFile 目录中搜索匹配的 AWB
        foreach (var (dir, _) in EnumerateResourceDirs("cueFile", null))
        {
            var cueXml = Path.Combine(dir, "CueFile.xml");
            if (!System.IO.File.Exists(cueXml)) continue;

            try
            {
                var cueDoc = new XmlDocument();
                cueDoc.Load(cueXml);
                var nameStr = cueDoc.SelectSingleNode("/CueFileData/name/str")?.InnerText;
                if (nameStr != cueStr) continue;

                var awbFileName = cueDoc.SelectSingleNode("/CueFileData/awbFile/path")?.InnerText;
                if (string.IsNullOrEmpty(awbFileName)) continue;

                var awbPath = Path.Combine(dir, awbFileName);
                if (System.IO.File.Exists(awbPath)) return awbPath;
            }
            catch { }
        }

        return null;
    }

    #endregion

    #region File Dialog

    [HttpPost]
    public ActionResult<string> OpenImageFileDialog()
    {
        string? selected = null;
        var thread = new Thread(() =>
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "PNG 图片|*.png|所有图片|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "选择图片"
            };
            if (dialog.ShowDialog() == DialogResult.OK)
                selected = dialog.FileName;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return Ok(selected ?? "");
    }

    [HttpGet]
    public ActionResult GetLocalImagePreview([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            return NotFound();

        var safe = PathGuard.EnsureWithin(StaticSettings.GamePath, path)
                   ?? PathGuard.EnsureWithin(StaticSettings.AppDataDir, path)
                   ?? PathGuard.EnsureWithin(StaticSettings.ExeDir, path);
        if (safe == null)
            return Forbid();

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var mime = ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream",
        };

        var bytes = System.IO.File.ReadAllBytes(path);
        return File(bytes, mime);
    }

    #endregion

    #region Trophy Rank Background

    /// <summary>
    /// CHU_UI_title_rank_00_v10.dds（768x1024）中的条目区域。
    /// 数据来自对贴图图集的逆向：(y, height, contentX, contentWidth)。
    /// </summary>
    private static readonly (int y, int h, int x, int w)[] TrophyAtlasStrips =
    [
        (7, 60, 7, 591),
        (82, 60, 7, 591),
        (157, 60, 7, 591),
        (232, 60, 7, 591),
        (307, 60, 7, 591),
        (382, 60, 7, 591),
        (457, 60, 7, 591),
        (532, 60, 7, 591),
        (607, 60, 7, 591),
        (682, 59, 8, 590),
        (757, 59, 8, 590),
        (832, 59, 8, 590),
        (907, 60, 7, 591),
    ];

    private static readonly Dictionary<int, int> RareTypeToStrip = new()
    {
        [0] = 0, [2] = 1, [3] = 2, [4] = 3, [5] = 4, [6] = 3, [7] = 5,
        [8] = 6, [9] = 7, [10] = 8, [11] = 9, [13] = 10, [14] = 11, [15] = 12,
    };

    private static byte[]? _trophyAtlasCache;
    private static int _trophyAtlasWidth;
    private static int _trophyAtlasHeight;
    private static int _trophyAtlasStride;
    private static readonly object TrophyAtlasLock = new();

    [HttpGet]
    public ActionResult GetTrophyRankBackground([FromQuery] int rareType)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return NotFound();

        var stripIndex = RareTypeToStrip.GetValueOrDefault(rareType, 0);
        if (stripIndex < 0 || stripIndex >= TrophyAtlasStrips.Length)
            stripIndex = 0;

        var strip = TrophyAtlasStrips[stripIndex];

        lock (TrophyAtlasLock)
        {
            if (_trophyAtlasCache == null)
            {
                var atlasPath = Path.Combine(StaticSettings.GamePath, "data", "surfboard", "texture",
                    "CHU_UI_title_rank_00_v10.dds");
                if (!System.IO.File.Exists(atlasPath))
                    return NotFound("Trophy atlas not found");

                try
                {
                    using var image = Pfim.Pfimage.FromFile(atlasPath);
                    if (image.Compressed) image.Decompress();

                    _trophyAtlasWidth = image.Width;
                    _trophyAtlasHeight = image.Height;
                    _trophyAtlasStride = image.Stride;
                    _trophyAtlasCache = new byte[image.Data.Length];
                    Array.Copy(image.Data, _trophyAtlasCache, image.Data.Length);
                }
                catch
                {
                    return StatusCode(500, "Failed to load trophy atlas");
                }
            }
        }

        try
        {
            if (strip.y + strip.h > _trophyAtlasHeight || strip.x + strip.w > _trophyAtlasWidth)
                return NotFound("Strip out of bounds");

            var pixelData = new byte[strip.w * strip.h * 4];
            for (var row = 0; row < strip.h; row++)
            {
                var srcOffset = (strip.y + row) * _trophyAtlasStride + strip.x * 4;
                var dstOffset = row * strip.w * 4;
                for (var col = 0; col < strip.w; col++)
                {
                    var si = srcOffset + col * 4;
                    var di = dstOffset + col * 4;
                    pixelData[di] = _trophyAtlasCache[si + 2];
                    pixelData[di + 1] = _trophyAtlasCache[si + 1];
                    pixelData[di + 2] = _trophyAtlasCache[si];
                    pixelData[di + 3] = _trophyAtlasCache[si + 3];
                }
            }

            using var img = SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(
                pixelData, strip.w, strip.h);
            using var ms = new MemoryStream();
            img.SaveAsPng(ms);
            return File(ms.ToArray(), "image/png");
        }
        catch
        {
            return StatusCode(500, "Failed to crop trophy strip");
        }
    }

    #endregion
}
