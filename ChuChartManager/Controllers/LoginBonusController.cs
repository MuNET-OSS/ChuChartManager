using System.Xml;
using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class LoginBonusController : ControllerBase
{
    #region DTOs

    public class PresetListItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string AssetDir { get; set; } = "";
        public int BonusCount { get; set; }
        public bool Disabled { get; set; }
    }

    public class BonusEntry
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int PresentId { get; set; }
        public string PresentName { get; set; } = "";
        public int ItemNum { get; set; }
        public int NeedLoginDayCount { get; set; }
        public int CategoryType { get; set; }
        public bool Disabled { get; set; }
    }

    public class PresetDetail
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string DataName { get; set; } = "";
        public string AssetDir { get; set; } = "";
        public bool Disabled { get; set; }
        public List<BonusEntry> Bonuses { get; set; } = [];
    }

    public class SavePresetDto
    {
        public string Name { get; set; } = "";
        public bool Disabled { get; set; }
        public List<SaveBonusDto> Bonuses { get; set; } = [];
    }

    public class SaveBonusDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int PresentId { get; set; }
        public string PresentName { get; set; } = "";
        public int ItemNum { get; set; } = 1;
        public int NeedLoginDayCount { get; set; } = 1;
        public int CategoryType { get; set; } = 1;
        public bool Disabled { get; set; }
    }

    public class CreatePresetDto
    {
        public string TargetDir { get; set; } = "";
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    #endregion

    #region List / Detail

    [HttpGet]
    public ActionResult<List<PresetListItem>> GetPresetList([FromQuery] string? source = null)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return Ok(new List<PresetListItem>());

        var result = new List<PresetListItem>();
        foreach (var (dir, assetDir) in EnumerateDirs("loginBonusPreset", source))
        {
            var xmlPath = Path.Combine(dir, "LoginBonusPreset.xml");
            if (!System.IO.File.Exists(xmlPath)) continue;

            try
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                var root = doc.SelectSingleNode("/LoginBonusPresetData");
                if (root == null) continue;

                var infos = root.SelectNodes("infos/LoginBonusDataInfo");
                result.Add(new PresetListItem
                {
                    Id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var id) ? id : 0,
                    Name = root.SelectSingleNode("name/str")?.InnerText ?? "",
                    AssetDir = assetDir,
                    BonusCount = infos?.Count ?? 0,
                    Disabled = bool.TryParse(root.SelectSingleNode("disableFlag")?.InnerText, out var d) && d,
                });
            }
            catch { }
        }

        return Ok(result.OrderBy(p => p.Id).ToList());
    }

    [HttpGet]
    public ActionResult<PresetDetail> GetPreset([FromQuery] int id, [FromQuery] string assetDir)
    {
        var xmlPath = FindXml("loginBonusPreset", "LoginBonusPreset.xml", "LoginBonusPresetData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var doc = new XmlDocument();
        doc.Load(xmlPath);
        var root = doc.SelectSingleNode("/LoginBonusPresetData");
        if (root == null) return NotFound();

        var bonuses = new List<BonusEntry>();
        var infoNodes = root.SelectNodes("infos/LoginBonusDataInfo");
        if (infoNodes != null)
        {
            foreach (XmlNode info in infoNodes)
            {
                var bonusId = int.TryParse(info.SelectSingleNode("loginBonusName/id")?.InnerText, out var bid) ? bid : 0;
                var bonusData = LoadBonusData(bonusId, assetDir);
                bonuses.Add(bonusData ?? new BonusEntry { Id = bonusId, Name = info.SelectSingleNode("loginBonusName/str")?.InnerText ?? "" });
            }
        }

        return Ok(new PresetDetail
        {
            Id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var pid) ? pid : 0,
            Name = root.SelectSingleNode("name/str")?.InnerText ?? "",
            DataName = root.SelectSingleNode("dataName")?.InnerText ?? "",
            AssetDir = assetDir,
            Disabled = bool.TryParse(root.SelectSingleNode("disableFlag")?.InnerText, out var d) && d,
            Bonuses = bonuses,
        });
    }

    private BonusEntry? LoadBonusData(int bonusId, string preferAssetDir)
    {
        var xmlPath = FindXml("loginBonus", "LoginBonus.xml", "LoginBonusData", bonusId, preferAssetDir)
                      ?? FindXml("loginBonus", "LoginBonus.xml", "LoginBonusData", bonusId, null);
        if (xmlPath == null) return null;

        var doc = new XmlDocument();
        doc.Load(xmlPath);
        var root = doc.SelectSingleNode("/LoginBonusData");
        if (root == null) return null;

        return new BonusEntry
        {
            Id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var bid) ? bid : 0,
            Name = root.SelectSingleNode("name/str")?.InnerText ?? "",
            PresentId = int.TryParse(root.SelectSingleNode("present/id")?.InnerText, out var pid) ? pid : 0,
            PresentName = root.SelectSingleNode("present/str")?.InnerText ?? "",
            ItemNum = int.TryParse(root.SelectSingleNode("itemNum")?.InnerText, out var inum) ? inum : 1,
            NeedLoginDayCount = int.TryParse(root.SelectSingleNode("needLoginDayCount")?.InnerText, out var nld) ? nld : 1,
            CategoryType = int.TryParse(root.SelectSingleNode("loginBonusCategoryType")?.InnerText, out var ct) ? ct : 1,
            Disabled = bool.TryParse(root.SelectSingleNode("disableFlag")?.InnerText, out var d) && d,
        };
    }

    #endregion

    #region Create / Save / Delete

    [HttpPost]
    public ActionResult CreatePreset([FromBody] CreatePresetDto dto)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("名称不能为空");

        var optionRoot = GetOptionRoot(dto.TargetDir);
        if (optionRoot == null) return BadRequest("Invalid target directory");

        var presetDir = Path.Combine(optionRoot, "loginBonusPreset", $"loginBonusPreset{dto.Id:D4}");
        if (Directory.Exists(presetDir))
            return BadRequest($"Preset {dto.Id} already exists");

        Directory.CreateDirectory(presetDir);

        var doc = CreateXmlDocument("LoginBonusPresetData");
        var root = (XmlElement)doc.DocumentElement!;

        AppendTextElement(doc, root, "dataName", $"loginBonusPreset{dto.Id:D4}");
        AppendTextElement(doc, root, "disableFlag", "false");
        AppendStringIdElement(doc, root, "name", dto.Id, dto.Name);
        AppendElement(doc, root, "infos", _ => { });

        doc.Save(Path.Combine(presetDir, "LoginBonusPreset.xml"));
        return Ok();
    }

    [HttpPost]
    public ActionResult SavePreset([FromQuery] int id, [FromQuery] string assetDir, [FromBody] SavePresetDto dto)
    {
        var xmlPath = FindXml("loginBonusPreset", "LoginBonusPreset.xml", "LoginBonusPresetData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var doc = new XmlDocument();
        doc.Load(xmlPath);
        var root = doc.SelectSingleNode("/LoginBonusPresetData");
        if (root == null) return NotFound();

        SetNodeText(root, "name/str", dto.Name);
        SetNodeText(root, "disableFlag", dto.Disabled.ToString().ToLower());

        var infosNode = root.SelectSingleNode("infos");
        if (infosNode != null)
        {
            infosNode.RemoveAll();
            foreach (var bonus in dto.Bonuses)
            {
                var infoEl = doc.CreateElement("LoginBonusDataInfo");
                AppendStringIdElement(doc, infoEl, "loginBonusName", bonus.Id, bonus.Name);
                infosNode.AppendChild(infoEl);

                SaveBonusXml(bonus, assetDir);
            }
        }

        doc.Save(xmlPath);
        return Ok();
    }

    private void SaveBonusXml(SaveBonusDto bonus, string assetDir)
    {
        var existingPath = FindXml("loginBonus", "LoginBonus.xml", "LoginBonusData", bonus.Id, assetDir);
        if (existingPath != null)
        {
            var doc = new XmlDocument();
            doc.Load(existingPath);
            var root = doc.SelectSingleNode("/LoginBonusData");
            if (root == null) return;

            SetNodeText(root, "disableFlag", bonus.Disabled.ToString().ToLower());
            SetNodeText(root, "name/str", bonus.Name);
            SetNodeText(root, "present/id", bonus.PresentId.ToString());
            SetNodeText(root, "present/str", bonus.PresentName);
            SetNodeText(root, "itemNum", bonus.ItemNum.ToString());
            SetNodeText(root, "needLoginDayCount", bonus.NeedLoginDayCount.ToString());
            SetNodeText(root, "loginBonusCategoryType", bonus.CategoryType.ToString());
            doc.Save(existingPath);
            return;
        }

        var optionRoot = GetOptionRoot(assetDir == "A000" ? assetDir : assetDir);
        if (optionRoot == null) return;

        var bonusDir = Path.Combine(optionRoot, "loginBonus", $"loginBonus{bonus.Id:D6}");
        Directory.CreateDirectory(bonusDir);

        var newDoc = CreateXmlDocument("LoginBonusData");
        var newRoot = (XmlElement)newDoc.DocumentElement!;

        AppendTextElement(newDoc, newRoot, "dataName", $"loginBonus{bonus.Id:D6}");
        AppendTextElement(newDoc, newRoot, "disableFlag", bonus.Disabled.ToString().ToLower());
        AppendStringIdElement(newDoc, newRoot, "name", bonus.Id, bonus.Name);
        AppendStringIdElement(newDoc, newRoot, "present", bonus.PresentId, bonus.PresentName);
        AppendTextElement(newDoc, newRoot, "itemNum", bonus.ItemNum.ToString());
        AppendTextElement(newDoc, newRoot, "needLoginDayCount", bonus.NeedLoginDayCount.ToString());
        AppendTextElement(newDoc, newRoot, "loginBonusCategoryType", bonus.CategoryType.ToString());

        newDoc.Save(Path.Combine(bonusDir, "LoginBonus.xml"));
    }

    [HttpPost]
    public ActionResult DeletePreset([FromQuery] int id, [FromQuery] string assetDir)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");

        var xmlPath = FindXml("loginBonusPreset", "LoginBonusPreset.xml", "LoginBonusPresetData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var dir = Path.GetDirectoryName(xmlPath)!;
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
            if (source != null && source != "A000" && source != dirName) continue;

            var resDir = Path.Combine(optDir, type);
            if (!Directory.Exists(resDir)) continue;

            foreach (var d in Directory.EnumerateDirectories(resDir))
                yield return (d, dirName);
        }
    }

    private static string? FindXml(string type, string xmlName, string rootNode, int id, string? assetDir)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath)) return null;

        if (assetDir != null)
        {
            string resRoot;
            if (assetDir == "A000")
                resRoot = Path.Combine(StaticSettings.GamePath, "data", "A000", type);
            else
            {
                var optionPath = OptionPathResolver.ResolveExisting(StaticSettings.GamePath, assetDir);
                if (optionPath == null) return null;
                resRoot = Path.Combine(optionPath, type);
            }

            if (Directory.Exists(resRoot))
            {
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
            }

            return null;
        }

        foreach (var (dir, _) in EnumerateDirs(type, null))
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
        if (string.IsNullOrEmpty(StaticSettings.GamePath)) return null;
        if (dirName == "A000") return Path.Combine(StaticSettings.GamePath, "data", "A000");
        return OptionPathResolver.ResolveWritePath(StaticSettings.GamePath, dirName);
    }

    private static void SetNodeText(XmlNode parent, string xpath, string value)
    {
        var node = parent.SelectSingleNode(xpath);
        if (node != null) node.InnerText = value;
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

    private static void AppendStringIdElement(XmlDocument doc, XmlElement parent, string name, int id, string str)
    {
        AppendElement(doc, parent, name, n =>
        {
            AppendTextElement(doc, n, "id", id.ToString());
            AppendTextElement(doc, n, "str", str);
            AppendElement(doc, n, "data", _ => { });
        });
    }

    #endregion
}
