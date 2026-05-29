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
    private const string NetOpenId = "2800";
    private const string NetOpenStr = "v2_45 00_0";

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
            musicRoot = Path.Combine(StaticSettings.GamePath, "bin", "option", assetDir, "music");

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

        var optionRoot = Path.Combine(StaticSettings.GamePath, "bin", "option");
        if (!Directory.Exists(optionRoot)) yield break;

        foreach (var optDir in Directory.EnumerateDirectories(optionRoot).OrderBy(d => d))
        {
            var dirName = Path.GetFileName(optDir);
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
            resRoot = Path.Combine(StaticSettings.GamePath, "bin", "option", assetDir, typeDef.type);

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
    /// Strip regions in CHU_UI_title_rank_00_v10.dds (768x1024).
    /// Values from reverse-engineering the surfboard texture atlas: (y, height, contentX, contentWidth).
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

    #region Trophy (称号)

    public class CreateTrophyDto
    {
        public string TargetDir { get; set; } = "";
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int RareType { get; set; }
        public string ExplainText { get; set; } = "";
        public string? ImagePath { get; set; }
    }

    [HttpPost]
    public ActionResult CreateTrophy([FromBody] CreateTrophyDto dto)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("名称不能为空");

        var optionRoot = GetOptionRoot(dto.TargetDir);
        if (optionRoot == null) return BadRequest("目标目录无效");

        var dirName = $"trophy{dto.Id:D6}";
        var targetDir = Path.Combine(optionRoot, "trophy", dirName);
        Directory.CreateDirectory(targetDir);

        var ddsFileName = "";
        if (!string.IsNullOrWhiteSpace(dto.ImagePath) && System.IO.File.Exists(dto.ImagePath))
        {
            ddsFileName = $"CHU_UI_Trophy_{dto.Id:D6}.dds";
            try
            {
                DdsHelper.ConvertPngToDds(dto.ImagePath, Path.Combine(targetDir, ddsFileName));
            }
            catch (Exception ex)
            {
                return BadRequest($"图片转换失败: {ex.Message}");
            }
        }

        var doc = CreateXmlDocument("TrophyData");
        var root = doc.DocumentElement!;

        AppendTextElement(doc, root, "dataName", dirName);
        AppendNetOpenName(doc, root);
        AppendTextElement(doc, root, "disableFlag", "false");
        AppendNameElement(doc, root, dto.Id, dto.Name);
        AppendTextElement(doc, root, "explainText", dto.ExplainText);
        AppendTextElement(doc, root, "defaultHave", "true");
        AppendTextElement(doc, root, "rareType", dto.RareType.ToString());
        AppendImageElement(doc, root, ddsFileName);
        AppendElement(doc, root, "normCondition", n =>
            AppendElement(doc, n, "conditions", _ => { }));
        AppendTextElement(doc, root, "priority", "0");

        doc.Save(Path.Combine(targetDir, "Trophy.xml"));
        return Ok();
    }

    #endregion

    #region NamePlate (名牌)

    public class CreateNamePlateDto
    {
        public string TargetDir { get; set; } = "";
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string ExplainText { get; set; } = "";
        public string ImagePath { get; set; } = "";
    }

    [HttpPost]
    public ActionResult CreateNamePlate([FromBody] CreateNamePlateDto dto)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("名称不能为空");
        if (string.IsNullOrWhiteSpace(dto.ImagePath) || !System.IO.File.Exists(dto.ImagePath))
            return BadRequest("请选择图片文件");

        var optionRoot = GetOptionRoot(dto.TargetDir);
        if (optionRoot == null) return BadRequest("目标目录无效");

        var dirName = $"namePlate{dto.Id:D8}";
        var ddsFileName = $"CHU_UI_NamePlate_{dto.Id:D8}.dds";
        var targetDir = Path.Combine(optionRoot, "namePlate", dirName);
        Directory.CreateDirectory(targetDir);

        try
        {
            DdsHelper.ConvertPngToDds(dto.ImagePath, Path.Combine(targetDir, ddsFileName));
        }
        catch (Exception ex)
        {
            return BadRequest($"图片转换失败: {ex.Message}");
        }

        var doc = CreateXmlDocument("NamePlateData");
        var root = doc.DocumentElement!;

        AppendTextElement(doc, root, "dataName", dirName);
        AppendNetOpenName(doc, root);
        AppendTextElement(doc, root, "disableFlag", "false");
        AppendNameElement(doc, root, dto.Id, dto.Name);
        AppendTextElement(doc, root, "sortName", GetSortName(dto.Name));
        AppendImageElement(doc, root, ddsFileName);
        AppendTextElement(doc, root, "defaultHave", "true");
        AppendTextElement(doc, root, "explainText", dto.ExplainText);
        AppendTextElement(doc, root, "priority", "0");

        doc.Save(Path.Combine(targetDir, "NamePlate.xml"));
        return Ok();
    }

    #endregion

    #region MapIcon (地图图标)

    public class CreateMapIconDto
    {
        public string TargetDir { get; set; } = "";
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string ExplainText { get; set; } = "";
        public string ImagePath { get; set; } = "";
    }

    [HttpPost]
    public ActionResult CreateMapIcon([FromBody] CreateMapIconDto dto)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("名称不能为空");
        if (string.IsNullOrWhiteSpace(dto.ImagePath) || !System.IO.File.Exists(dto.ImagePath))
            return BadRequest("请选择图片文件");

        var optionRoot = GetOptionRoot(dto.TargetDir);
        if (optionRoot == null) return BadRequest("目标目录无效");

        var dirName = $"mapIcon{dto.Id:D4}";
        var ddsFileName = $"CHU_UI_MapIcon_{dto.Id:D8}.dds";
        var targetDir = Path.Combine(optionRoot, "mapIcon", dirName);
        Directory.CreateDirectory(targetDir);

        try
        {
            DdsHelper.ConvertPngToDds(dto.ImagePath, Path.Combine(targetDir, ddsFileName));
        }
        catch (Exception ex)
        {
            return BadRequest($"图片转换失败: {ex.Message}");
        }

        var doc = CreateXmlDocument("MapIconData");
        var root = doc.DocumentElement!;

        AppendTextElement(doc, root, "dataName", dirName);
        AppendNetOpenName(doc, root);
        AppendTextElement(doc, root, "disableFlag", "false");
        AppendNameElement(doc, root, dto.Id, dto.Name);
        AppendTextElement(doc, root, "sortName", GetSortName(dto.Name));
        AppendImageElement(doc, root, ddsFileName);
        AppendTextElement(doc, root, "defaultHave", "true");
        AppendTextElement(doc, root, "explainText", dto.ExplainText);
        AppendTextElement(doc, root, "priority", "0");

        doc.Save(Path.Combine(targetDir, "MapIcon.xml"));
        return Ok();
    }

    #endregion

    #region AvatarAccessory (头像挂件)

    public class CreateAvatarAccessoryDto
    {
        public string TargetDir { get; set; } = "";
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string ExplainText { get; set; } = "";
        public int Category { get; set; } = 1;
        public string IconImagePath { get; set; } = "";
        public string TextureImagePath { get; set; } = "";
    }

    [HttpPost]
    public ActionResult CreateAvatarAccessory([FromBody] CreateAvatarAccessoryDto dto)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("名称不能为空");
        if (string.IsNullOrWhiteSpace(dto.IconImagePath) || !System.IO.File.Exists(dto.IconImagePath))
            return BadRequest("请选择图标图片");
        if (string.IsNullOrWhiteSpace(dto.TextureImagePath) || !System.IO.File.Exists(dto.TextureImagePath))
            return BadRequest("请选择贴图图片");

        var optionRoot = GetOptionRoot(dto.TargetDir);
        if (optionRoot == null) return BadRequest("目标目录无效");

        var dirName = $"avatarAccessory{dto.Id:D8}";
        var iconDdsName = $"CHU_UI_Avatar_Icon_{dto.Id:D8}.dds";
        var textureDdsName = $"CHU_UI_Avatar_Tex_{dto.Id:D8}.dds";
        var targetDir = Path.Combine(optionRoot, "avatarAccessory", dirName);
        Directory.CreateDirectory(targetDir);

        try
        {
            DdsHelper.ConvertPngToDds(dto.IconImagePath, Path.Combine(targetDir, iconDdsName));
            DdsHelper.ConvertPngToDds(dto.TextureImagePath, Path.Combine(targetDir, textureDdsName));
        }
        catch (Exception ex)
        {
            return BadRequest($"图片转换失败: {ex.Message}");
        }

        var doc = CreateXmlDocument("AvatarAccessoryData");
        var root = doc.DocumentElement!;

        AppendTextElement(doc, root, "dataName", dirName);
        AppendNetOpenName(doc, root);
        AppendTextElement(doc, root, "disableFlag", "false");
        AppendNameElement(doc, root, dto.Id, dto.Name);
        AppendTextElement(doc, root, "sortName", GetSortName(dto.Name));
        AppendTextElement(doc, root, "category", dto.Category.ToString());
        AppendImageElement(doc, root, iconDdsName);
        AppendElement(doc, root, "texture", n =>
            AppendTextElement(doc, n, "path", textureDdsName));
        AppendTextElement(doc, root, "defaultHave", "true");
        AppendTextElement(doc, root, "explainText", dto.ExplainText);
        AppendTextElement(doc, root, "priority", "0");

        doc.Save(Path.Combine(targetDir, "AvatarAccessory.xml"));
        return Ok();
    }

    #endregion

    #region Chara (角色)

    public class CreateCharaDto
    {
        public string TargetDir { get; set; } = "";
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Works { get; set; } = "CHUNITHM";
        public string Illustrator { get; set; } = "";
        public string ImagePath { get; set; } = "";
        public string ImagePathMid { get; set; } = "";
        public string ImagePathSmall { get; set; } = "";
    }

    private static readonly (int size, string suffix)[] CharaImageSizes =
    [
        (1080, "_00"),  // ddsFile0: 全身 1080x1080
        (512, "_01"),   // ddsFile1: 半身 512x512
        (128, "_02"),   // ddsFile2: 大头 128x128
    ];

    [HttpPost]
    public ActionResult CreateChara([FromBody] CreateCharaDto dto)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("名称不能为空");
        if (string.IsNullOrWhiteSpace(dto.ImagePath) || !System.IO.File.Exists(dto.ImagePath))
            return BadRequest("请选择全身图片");
        if (string.IsNullOrWhiteSpace(dto.ImagePathMid) || !System.IO.File.Exists(dto.ImagePathMid))
            return BadRequest("请选择半身图片");
        if (string.IsNullOrWhiteSpace(dto.ImagePathSmall) || !System.IO.File.Exists(dto.ImagePathSmall))
            return BadRequest("请选择头像图片");

        var optionRoot = GetOptionRoot(dto.TargetDir);
        if (optionRoot == null) return BadRequest("目标目录无效");

        var charaDirName = $"chara{dto.Id:D6}";
        var charaDir = Path.Combine(optionRoot, "chara", charaDirName);
        Directory.CreateDirectory(charaDir);

        var ddsImageDirName = $"ddsImage{dto.Id:D6}";
        var ddsImageDir = Path.Combine(optionRoot, "ddsImage", ddsImageDirName);
        Directory.CreateDirectory(ddsImageDir);

        var imageIdMajor = dto.Id / 10;
        var imageIdMinor = dto.Id % 10;
        var imageBaseName = $"chara{imageIdMajor:D4}_{imageIdMinor:D2}";
        var ddsFilePrefix = $"CHU_UI_Character_{imageIdMajor:D4}_{imageIdMinor:D2}";

        var sourcePaths = new[] { dto.ImagePath, dto.ImagePathMid, dto.ImagePathSmall };

        var ddsFileNames = new string[3];
        try
        {
            for (var i = 0; i < CharaImageSizes.Length; i++)
            {
                var (size, suffix) = CharaImageSizes[i];
                var ddsFileName = $"{ddsFilePrefix}{suffix}.dds";
                ddsFileNames[i] = ddsFileName;
                DdsHelper.ConvertPngToDdsResized(sourcePaths[i], Path.Combine(ddsImageDir, ddsFileName), size, size);
            }
        }
        catch (Exception ex)
        {
            return BadRequest($"图片转换失败: {ex.Message}");
        }

        var ddsDoc = CreateXmlDocument("DDSImageData");
        var ddsRoot = ddsDoc.DocumentElement!;
        AppendTextElement(ddsDoc, ddsRoot, "dataName", ddsImageDirName);
        AppendElement(ddsDoc, ddsRoot, "name", n =>
        {
            AppendTextElement(ddsDoc, n, "id", dto.Id.ToString());
            AppendTextElement(ddsDoc, n, "str", imageBaseName);
            AppendElement(ddsDoc, n, "data", _ => { });
        });
        for (var i = 0; i < 3; i++)
        {
            var idx = i;
            AppendElement(ddsDoc, ddsRoot, $"ddsFile{idx}", n =>
                AppendTextElement(ddsDoc, n, "path", ddsFileNames[idx]));
        }
        AppendNetOpenName(ddsDoc, ddsRoot);
        ddsDoc.Save(Path.Combine(ddsImageDir, "DDSImage.xml"));

        var doc = CreateXmlDocument("CharaData");
        var root = doc.DocumentElement!;

        AppendTextElement(doc, root, "dataName", charaDirName);
        AppendElement(doc, root, "releaseTagName", n =>
        {
            AppendTextElement(doc, n, "id", "0");
            AppendTextElement(doc, n, "str", "v1 1.00.00");
            AppendElement(doc, n, "data", _ => { });
        });
        AppendNetOpenName(doc, root);
        AppendTextElement(doc, root, "disableFlag", "false");
        AppendNameElement(doc, root, dto.Id, dto.Name);
        AppendTextElement(doc, root, "explainText", "");
        AppendTextElement(doc, root, "sortName", GetSortName(dto.Name));
        AppendElement(doc, root, "works", n =>
        {
            AppendTextElement(doc, n, "id", "9000");
            AppendTextElement(doc, n, "str", dto.Works);
            AppendElement(doc, n, "data", _ => { });
        });
        AppendElement(doc, root, "illustratorName", n =>
        {
            AppendTextElement(doc, n, "id", "0");
            AppendTextElement(doc, n, "str", dto.Illustrator);
            AppendElement(doc, n, "data", _ => { });
        });
        AppendTextElement(doc, root, "defaultHave", "true");
        AppendTextElement(doc, root, "rareType", "0");
        AppendElement(doc, root, "normCondition", n =>
            AppendElement(doc, n, "conditions", _ => { }));
        AppendTextElement(doc, root, "ranking", "true");

        AppendElement(doc, root, "defaultImages", n =>
        {
            AppendTextElement(doc, n, "id", dto.Id.ToString());
            AppendTextElement(doc, n, "str", imageBaseName);
            AppendElement(doc, n, "data", _ => { });
        });

        // addImages1~9 全部设为无效
        for (var i = 1; i <= 9; i++)
        {
            AppendElement(doc, root, $"addImages{i}", n =>
            {
                AppendTextElement(doc, n, "changeImg", "false");
                AppendElement(doc, n, "charaName", cn =>
                {
                    AppendTextElement(doc, cn, "id", "-1");
                    AppendTextElement(doc, cn, "str", "Invalid");
                    AppendElement(doc, cn, "data", _ => { });
                });
                AppendElement(doc, n, "image", img =>
                {
                    AppendTextElement(doc, img, "id", "-1");
                    AppendTextElement(doc, img, "str", "Invalid");
                    AppendElement(doc, img, "data", _ => { });
                });
                AppendTextElement(doc, n, "rank", "1");
            });
        }

        AppendTextElement(doc, root, "priority", "0");
        AppendElement(doc, root, "ranks", _ => { });

        doc.Save(Path.Combine(charaDir, "Chara.xml"));
        return Ok();
    }

    public class AddCharaVariantDto
    {
        public string TargetDir { get; set; } = "";
        public int BaseId { get; set; }
        public int Variant { get; set; }
        public string Name { get; set; } = "";
        public string ImagePath { get; set; } = "";
        public string ImagePathMid { get; set; } = "";
        public string ImagePathSmall { get; set; } = "";
        public int Rank { get; set; } = 1;
    }

    [HttpPost]
    public ActionResult AddCharaVariant([FromBody] AddCharaVariantDto dto)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");
        if (dto.Variant < 1 || dto.Variant > 9)
            return BadRequest("Variant must be 1-9");
        if (string.IsNullOrWhiteSpace(dto.ImagePath) || !System.IO.File.Exists(dto.ImagePath))
            return BadRequest("请选择全身图片");
        if (string.IsNullOrWhiteSpace(dto.ImagePathMid) || !System.IO.File.Exists(dto.ImagePathMid))
            return BadRequest("请选择半身图片");
        if (string.IsNullOrWhiteSpace(dto.ImagePathSmall) || !System.IO.File.Exists(dto.ImagePathSmall))
            return BadRequest("请选择头像图片");

        var variantId = dto.BaseId * 10 + dto.Variant;

        var optionRoot = GetOptionRoot(dto.TargetDir);
        if (optionRoot == null) return BadRequest("目标目录无效");

        var ddsImageDirName = $"ddsImage{variantId:D6}";
        var ddsImageDir = Path.Combine(optionRoot, "ddsImage", ddsImageDirName);
        Directory.CreateDirectory(ddsImageDir);

        var imageIdMajor = variantId / 10;
        var imageIdMinor = variantId % 10;
        var imageBaseName = $"chara{imageIdMajor:D4}_{imageIdMinor:D2}";
        var ddsFilePrefix = $"CHU_UI_Character_{imageIdMajor:D4}_{imageIdMinor:D2}";

        var sourcePaths = new[] { dto.ImagePath, dto.ImagePathMid, dto.ImagePathSmall };

        var ddsFileNames = new string[3];
        try
        {
            for (var i = 0; i < CharaImageSizes.Length; i++)
            {
                var (size, suffix) = CharaImageSizes[i];
                var ddsFileName = $"{ddsFilePrefix}{suffix}.dds";
                ddsFileNames[i] = ddsFileName;
                DdsHelper.ConvertPngToDdsResized(sourcePaths[i], Path.Combine(ddsImageDir, ddsFileName), size, size);
            }
        }
        catch (Exception ex)
        {
            return BadRequest($"图片转换失败: {ex.Message}");
        }

        var ddsDoc = CreateXmlDocument("DDSImageData");
        var ddsRoot = ddsDoc.DocumentElement!;
        AppendTextElement(ddsDoc, ddsRoot, "dataName", ddsImageDirName);
        AppendElement(ddsDoc, ddsRoot, "name", n =>
        {
            AppendTextElement(ddsDoc, n, "id", variantId.ToString());
            AppendTextElement(ddsDoc, n, "str", imageBaseName);
            AppendElement(ddsDoc, n, "data", _ => { });
        });
        for (var i = 0; i < 3; i++)
        {
            var idx = i;
            AppendElement(ddsDoc, ddsRoot, $"ddsFile{idx}", n =>
                AppendTextElement(ddsDoc, n, "path", ddsFileNames[idx]));
        }
        AppendNetOpenName(ddsDoc, ddsRoot);
        ddsDoc.Save(Path.Combine(ddsImageDir, "DDSImage.xml"));

        // 写回主角色 Chara.xml 的 addImages{variant} 节点
        var charaTypeDef = ResourceTypes.First(t => t.type == "chara");
        var baseCharaId = dto.BaseId * 10; // variant 0 = base chara
        var charaDir = FindResourceDir(charaTypeDef, baseCharaId, dto.TargetDir);
        if (charaDir == null)
            return Ok(new { warning = "升格图片已生成，但未找到主角色目录，请手动关联" });

        var charaXmlPath = Path.Combine(charaDir, "Chara.xml");
        if (!System.IO.File.Exists(charaXmlPath))
            return Ok(new { warning = "升格图片已生成，但主角色 XML 不存在" });

        try
        {
            var charaDoc = new XmlDocument();
            charaDoc.Load(charaXmlPath);
            var charaRoot = charaDoc.SelectSingleNode("/CharaData") as XmlElement;
            if (charaRoot == null)
                return Ok(new { warning = "升格图片已生成，但 XML 格式异常" });

            var addImagesNode = charaRoot.SelectSingleNode($"addImages{dto.Variant}") as XmlElement;
            if (addImagesNode != null)
            {
                addImagesNode.RemoveAll();
                AppendTextElement(charaDoc, addImagesNode, "changeImg", "true");
                AppendElement(charaDoc, addImagesNode, "charaName", cn =>
                {
                    AppendTextElement(charaDoc, cn, "id", variantId.ToString());
                    AppendTextElement(charaDoc, cn, "str", dto.Name);
                    AppendElement(charaDoc, cn, "data", _ => { });
                });
                AppendElement(charaDoc, addImagesNode, "image", img =>
                {
                    AppendTextElement(charaDoc, img, "id", variantId.ToString());
                    AppendTextElement(charaDoc, img, "str", imageBaseName);
                    AppendElement(charaDoc, img, "data", _ => { });
                });
                AppendTextElement(charaDoc, addImagesNode, "rank", dto.Rank.ToString());
            }

            charaDoc.Save(charaXmlPath);
        }
        catch (Exception ex)
        {
            return Ok(new { warning = $"升格图片已生成，但写回主角色失败: {ex.Message}" });
        }

        return Ok();
    }

    #endregion

    #region XML Helpers

    private static string? GetOptionRoot(string dirName)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath) || string.IsNullOrWhiteSpace(dirName))
            return null;

        // data 目录下的 opt（如 A000）
        var dataPath = Path.Combine(StaticSettings.GamePath, "data", dirName);
        if (Directory.Exists(dataPath))
            return dataPath;

        // bin/option 目录下的 opt（如 A001）
        var optionPath = Path.Combine(StaticSettings.GamePath, "bin", "option", dirName);
        if (Directory.Exists(optionPath))
            return optionPath;

        // 如果都不存在，在 bin/option 下创建
        Directory.CreateDirectory(optionPath);
        return optionPath;
    }

    private static string GetSortName(string name)
    {
        // 取第一个字符作为排序名
        return string.IsNullOrEmpty(name) ? "" : name[..1];
    }

    private static XmlDocument CreateXmlDocument(string rootElementName)
    {
        var doc = new XmlDocument();
        var decl = doc.CreateXmlDeclaration("1.0", "utf-8", null);
        doc.AppendChild(decl);

        var root = doc.CreateElement(rootElementName);
        root.SetAttribute("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");
        root.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
        doc.AppendChild(root);

        return doc;
    }

    private static void AppendTextElement(XmlDocument doc, XmlElement parent, string name, string value)
    {
        var el = doc.CreateElement(name);
        el.InnerText = value;
        parent.AppendChild(el);
    }

    private static void AppendElement(XmlDocument doc, XmlElement parent, string name, Action<XmlElement> build)
    {
        var el = doc.CreateElement(name);
        build(el);
        parent.AppendChild(el);
    }

    private static void AppendNetOpenName(XmlDocument doc, XmlElement parent)
    {
        AppendElement(doc, parent, "netOpenName", n =>
        {
            AppendTextElement(doc, n, "id", NetOpenId);
            AppendTextElement(doc, n, "str", NetOpenStr);
            AppendElement(doc, n, "data", _ => { });
        });
    }

    private static void AppendNameElement(XmlDocument doc, XmlElement parent, int id, string name)
    {
        AppendElement(doc, parent, "name", n =>
        {
            AppendTextElement(doc, n, "id", id.ToString());
            AppendTextElement(doc, n, "str", name);
            AppendElement(doc, n, "data", _ => { });
        });
    }

    private static void AppendImageElement(XmlDocument doc, XmlElement parent, string path)
    {
        AppendElement(doc, parent, "image", n =>
            AppendTextElement(doc, n, "path", path));
    }

    #endregion
}
