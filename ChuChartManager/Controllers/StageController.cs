using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Xml;
using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class StageController : ControllerBase
{
    #region DTOs

    public class StageListItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string AssetDir { get; set; } = "";
        public bool HasImage { get; set; }
    }

    public class StageDetail
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string DataName { get; set; } = "";
        public string AssetDir { get; set; } = "";
        public string NotesFieldLine { get; set; } = "";
        public int NotesFieldLineId { get; set; }
        public bool DefaultHave { get; set; }
    }

    public class CreateStageDto
    {
        public string TargetDir { get; set; } = "";
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string ImagePath { get; set; } = "";
        public int NotesFieldLineId { get; set; }
        public string NotesFieldLine { get; set; } = "Orange";
    }

    public class SaveStageDto
    {
        public string Name { get; set; } = "";
        public int NotesFieldLineId { get; set; }
        public string NotesFieldLine { get; set; } = "";
    }

    #endregion

    #region List / Detail

    [HttpGet]
    public ActionResult<List<StageListItem>> GetStageList([FromQuery] string? source = null)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return Ok(new List<StageListItem>());

        var result = new List<StageListItem>();
        foreach (var (dir, assetDir) in EnumerateDirs("stage", source))
        {
            var xmlPath = Path.Combine(dir, "Stage.xml");
            if (!System.IO.File.Exists(xmlPath)) continue;

            try
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                var root = doc.SelectSingleNode("/StageData");
                if (root == null) continue;

                result.Add(new StageListItem
                {
                    Id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var id) ? id : 0,
                    Name = root.SelectSingleNode("name/str")?.InnerText ?? "",
                    AssetDir = assetDir,
                    HasImage = Directory.EnumerateFiles(dir, "*.dds").Any(),
                });
            }
            catch { }
        }

        return Ok(result.OrderBy(s => s.Id).ToList());
    }

    [HttpGet]
    public ActionResult<StageDetail> GetStage([FromQuery] int id, [FromQuery] string assetDir)
    {
        var xmlPath = FindXml("stage", "Stage.xml", "StageData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var doc = new XmlDocument();
        doc.Load(xmlPath);
        var root = doc.SelectSingleNode("/StageData");
        if (root == null) return NotFound();

        return Ok(new StageDetail
        {
            Id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var sid) ? sid : 0,
            Name = root.SelectSingleNode("name/str")?.InnerText ?? "",
            DataName = root.SelectSingleNode("dataName")?.InnerText ?? "",
            AssetDir = assetDir,
            NotesFieldLineId = int.TryParse(root.SelectSingleNode("notesFieldLine/id")?.InnerText, out var nid) ? nid : 0,
            NotesFieldLine = root.SelectSingleNode("notesFieldLine/str")?.InnerText ?? "",
            DefaultHave = bool.TryParse(root.SelectSingleNode("defaultHave")?.InnerText, out var dh) && dh,
        });
    }

    [HttpGet]
    public ActionResult GetStagePreview([FromQuery] int id, [FromQuery] string assetDir)
    {
        var xmlPath = FindXml("stage", "Stage.xml", "StageData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var dir = Path.GetDirectoryName(xmlPath)!;
        var ddsFile = Directory.EnumerateFiles(dir, "*.dds").FirstOrDefault();
        if (ddsFile == null) return NotFound();

        var pngData = ConvertDdsToPng(ddsFile);
        if (pngData == null) return NotFound();
        return File(pngData, "image/png");
    }

    #endregion

    #region Create / Save / Delete

    [HttpPost]
    public ActionResult CreateStage([FromBody] CreateStageDto dto)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("名称不能为空");
        if (string.IsNullOrWhiteSpace(dto.ImagePath) || !System.IO.File.Exists(dto.ImagePath))
            return BadRequest("请选择图片文件");

        var optionRoot = GetOptionRoot(dto.TargetDir);
        if (optionRoot == null) return BadRequest("目标目录无效");

        var dirName = $"stage{dto.Id:D6}";
        var ddsFileName = $"CHU_UI_Stage_{dto.Id:D5}.dds";
        var targetDir = Path.Combine(optionRoot, "stage", dirName);
        Directory.CreateDirectory(targetDir);

        try
        {
            DdsHelper.ConvertPngToDds(dto.ImagePath, Path.Combine(targetDir, ddsFileName));
        }
        catch (Exception ex)
        {
            return BadRequest($"图片转换失败: {ex.Message}");
        }

        var doc = CreateXmlDocument("StageData");
        var root = doc.DocumentElement!;

        AppendTextElement(doc, root, "dataName", dirName);
        AppendStringIdElement(doc, root, "netOpenName", 2800, "v2_45 00_0", "");
        AppendTextElement(doc, root, "disableFlag", "false");
        AppendStringIdElement(doc, root, "releaseTagName", 0, "v1 1.00.00", "");
        AppendStringIdElement(doc, root, "name", dto.Id, dto.Name, "");
        AppendStringIdElement(doc, root, "notesFieldLine", dto.NotesFieldLineId, dto.NotesFieldLine, "");
        AppendElement(doc, root, "notesFieldFile", n => AppendTextElement(doc, n, "path", ""));
        AppendElement(doc, root, "baseFile", n => AppendTextElement(doc, n, "path", ""));
        AppendElement(doc, root, "objectFile", n => AppendTextElement(doc, n, "path", ""));
        AppendTextElement(doc, root, "defaultHave", "true");
        AppendElement(doc, root, "image", n => AppendTextElement(doc, n, "path", ddsFileName));
        AppendTextElement(doc, root, "enablePlate", "true");
        AppendTextElement(doc, root, "sortName", "");
        AppendTextElement(doc, root, "priority", "0");

        doc.Save(Path.Combine(targetDir, "Stage.xml"));
        return Ok();
    }

    [HttpPost]
    public ActionResult SaveStage([FromQuery] int id, [FromQuery] string assetDir, [FromBody] SaveStageDto dto)
    {
        var xmlPath = FindXml("stage", "Stage.xml", "StageData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var doc = new XmlDocument();
        doc.Load(xmlPath);
        var root = doc.SelectSingleNode("/StageData") as XmlElement;
        if (root == null) return NotFound();

        SetNodeText(root, "name/str", dto.Name);
        SetNodeText(root, "notesFieldLine/id", dto.NotesFieldLineId.ToString());
        SetNodeText(root, "notesFieldLine/str", dto.NotesFieldLine);

        doc.Save(xmlPath);
        return Ok();
    }

    [HttpPost]
    public ActionResult DeleteStage([FromQuery] int id, [FromQuery] string assetDir)
    {
        var xmlPath = FindXml("stage", "Stage.xml", "StageData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var dir = Path.GetDirectoryName(xmlPath);
        if (dir != null && Directory.Exists(dir))
            Directory.Delete(dir, true);
        return Ok();
    }

    #endregion

    #region Helpers

    private static IEnumerable<(string dir, string assetDir)> EnumerateDirs(string type, string? source)
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

    private static string? FindXml(string type, string xmlName, string rootNode, int id, string assetDir)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath)) return null;

        string resRoot;
        if (assetDir == "A000")
            resRoot = Path.Combine(StaticSettings.GamePath, "data", "A000", type);
        else
        {
            var optionPath = OptionPathResolver.ResolveExisting(StaticSettings.GamePath, assetDir);
            if (optionPath == null) return null;
            resRoot = Path.Combine(optionPath, type);
        }

        if (!Directory.Exists(resRoot)) return null;

        foreach (var dir in Directory.EnumerateDirectories(resRoot))
        {
            var xmlPath = Path.Combine(dir, xmlName);
            if (!System.IO.File.Exists(xmlPath)) continue;
            try
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                var rid = doc.SelectSingleNode($"/{rootNode}/name/id")?.InnerText;
                if (rid != null && int.TryParse(rid, out var parsed) && parsed == id)
                    return xmlPath;
            }
            catch { }
        }
        return null;
    }

    private static string? GetOptionRoot(string dirName)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath) || string.IsNullOrWhiteSpace(dirName)) return null;
        var dataPath = Path.Combine(StaticSettings.GamePath, "data", dirName);
        if (Directory.Exists(dataPath)) return dataPath;
        var optionPath = OptionPathResolver.ResolveWritePath(StaticSettings.GamePath, dirName);
        Directory.CreateDirectory(optionPath);
        return optionPath;
    }

    private static void SetNodeText(XmlNode parent, string xpath, string value)
    {
        var node = parent.SelectSingleNode(xpath);
        if (node != null) node.InnerText = value;
    }

    private static readonly object PreviewLock = new();

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
                var bmpData = bitmap.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, pixelFormat);
                var copyLen = Math.Min(image.Data.Length, Math.Abs(bmpData.Stride) * image.Height);
                Marshal.Copy(image.Data, 0, bmpData.Scan0, copyLen);
                bitmap.UnlockBits(bmpData);
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                bitmap.Dispose();
                return ms.ToArray();
            }
            catch { return null; }
        }
    }

    private static XmlDocument CreateXmlDocument(string rootElementName)
    {
        var doc = new XmlDocument();
        doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
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

    private static void AppendStringIdElement(XmlDocument doc, XmlElement parent, string name, int id, string str, string data)
    {
        AppendElement(doc, parent, name, n =>
        {
            AppendTextElement(doc, n, "id", id.ToString());
            AppendTextElement(doc, n, "str", str);
            if (!string.IsNullOrEmpty(data))
                AppendTextElement(doc, n, "data", data);
            else
                AppendElement(doc, n, "data", _ => { });
        });
    }

    #endregion
}
