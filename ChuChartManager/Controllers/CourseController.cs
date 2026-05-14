using System.Xml;
using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class CourseController : ControllerBase
{
    #region DTOs

    public class CourseListItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public int DifficultyId { get; set; }
        public int MusicCount { get; set; }
        public string AssetDir { get; set; } = "";
        public string DataName { get; set; } = "";
    }

    public class CourseMusicInfo
    {
        public int Type { get; set; }
        public int MusicId { get; set; }
        public string MusicName { get; set; } = "";
        public int DiffId { get; set; }
        public string DiffName { get; set; } = "";
    }

    public class CourseDetail
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string DataName { get; set; } = "";
        public string AssetDir { get; set; } = "";
        public int DifficultyId { get; set; }
        public string Difficulty { get; set; } = "";
        public int RuleId { get; set; }
        public string RuleName { get; set; } = "";
        public int RewardId { get; set; }
        public string RewardName { get; set; } = "";
        public int Reward2ndId { get; set; }
        public string Reward2ndName { get; set; } = "";
        public bool TeamOnly { get; set; }
        public bool IsMusicDuplicateAllowed { get; set; }
        public int ConditionsCourseId { get; set; }
        public string ConditionsCourseName { get; set; } = "";
        public string ConditionsText { get; set; } = "";
        public int Priority { get; set; }
        public List<CourseMusicInfo> Musics { get; set; } = [];
    }

    public class CreateCourseDto
    {
        public string TargetDir { get; set; } = "";
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int DifficultyId { get; set; } = 10;
        public string Difficulty { get; set; } = "CLASS Ⅰ";
        public int RuleId { get; set; } = 34;
        public List<CreateCourseMusicDto> Musics { get; set; } = [];
    }

    public class CreateCourseMusicDto
    {
        public int MusicId { get; set; }
        public string MusicName { get; set; } = "";
        public int DiffId { get; set; } = 3;
        public string DiffName { get; set; } = "Master";
        public string DiffData { get; set; } = "MASTER";
    }

    public class SaveCourseDto
    {
        public string Name { get; set; } = "";
        public int DifficultyId { get; set; }
        public string Difficulty { get; set; } = "";
        public int RuleId { get; set; }
        public int RewardId { get; set; }
        public string RewardName { get; set; } = "";
        public int Reward2ndId { get; set; }
        public string Reward2ndName { get; set; } = "";
        public bool TeamOnly { get; set; }
        public bool IsMusicDuplicateAllowed { get; set; }
        public int ConditionsCourseId { get; set; }
        public string ConditionsCourseName { get; set; } = "";
        public string ConditionsText { get; set; } = "";
        public int Priority { get; set; }
        public List<CreateCourseMusicDto> Musics { get; set; } = [];
    }

    #endregion

    #region List / Detail

    [HttpGet]
    public ActionResult<List<CourseListItem>> GetCourseList([FromQuery] string? source = null)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return Ok(new List<CourseListItem>());

        var result = new List<CourseListItem>();
        var dirs = GetCourseDirectories(source);

        foreach (var (courseDir, assetDir) in dirs)
        {
            var xmlPath = Path.Combine(courseDir, "Course.xml");
            if (!System.IO.File.Exists(xmlPath)) continue;

            try
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                var root = doc.SelectSingleNode("/CourseData");
                if (root == null) continue;

                var id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var cid) ? cid : 0;
                var name = root.SelectSingleNode("name/str")?.InnerText ?? "";
                var diffId = int.TryParse(root.SelectSingleNode("difficulty/id")?.InnerText, out var did) ? did : 0;
                var diff = root.SelectSingleNode("difficulty/data")?.InnerText ?? "";
                var dataName = root.SelectSingleNode("dataName")?.InnerText ?? "";
                var musicCount = root.SelectNodes("infos/CourseMusicDataInfo")?.Count ?? 0;

                result.Add(new CourseListItem
                {
                    Id = id,
                    Name = name,
                    DifficultyId = diffId,
                    Difficulty = diff,
                    MusicCount = musicCount,
                    AssetDir = assetDir,
                    DataName = dataName,
                });
            }
            catch (Exception ex)
            {
                Log.Error($"加载 Course XML 失败: {xmlPath}", ex);
            }
        }

        return Ok(result.OrderBy(c => c.Id).ToList());
    }

    [HttpGet]
    public ActionResult<CourseDetail> GetCourse([FromQuery] int id, [FromQuery] string assetDir)
    {
        var xmlPath = FindCourseXml(id, assetDir);
        if (xmlPath == null) return NotFound();

        try
        {
            var doc = new XmlDocument();
            doc.Load(xmlPath);
            var root = doc.SelectSingleNode("/CourseData");
            if (root == null) return NotFound();

            var detail = ParseCourseDetail(root, assetDir);
            return Ok(detail);
        }
        catch (Exception ex)
        {
            return BadRequest($"读取 Course 失败: {ex.Message}");
        }
    }

    #endregion

    #region Create

    [HttpPost]
    public ActionResult CreateCourse([FromBody] CreateCourseDto dto)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("名称不能为空");
        if (dto.Musics.Count == 0)
            return BadRequest("至少需要一首曲目");

        var optionRoot = GetOptionRoot(dto.TargetDir);
        if (optionRoot == null) return BadRequest("目标目录无效");

        var dirName = $"course{dto.Id:D8}";
        var targetDir = Path.Combine(optionRoot, "course", dirName);
        Directory.CreateDirectory(targetDir);

        var doc = CreateXmlDocument("CourseData");
        var root = doc.DocumentElement!;

        AppendTextElement(doc, root, "dataName", dirName);
        AppendStringIdElement(doc, root, "releaseTagName", 0, "v1 1.00.00", "");
        AppendStringIdElement(doc, root, "netOpenName", 2800, "v2_45 00_0", "");
        AppendTextElement(doc, root, "disableFlag", "false");
        AppendStringIdElement(doc, root, "name", dto.Id, dto.Name, "");

        var diffStr = $"ID_{dto.DifficultyId}";
        AppendStringIdElement(doc, root, "difficulty", dto.DifficultyId, diffStr, dto.Difficulty);
        AppendStringIdElement(doc, root, "rule", dto.RuleId, $"{dto.RuleId:D4}", "");
        AppendStringIdElement(doc, root, "reward", 0, "なし", "");
        AppendStringIdElement(doc, root, "reward2nd", 0, "なし", "");

        AppendTextElement(doc, root, "teamOnly", "false");
        AppendTextElement(doc, root, "isMusicDuplicateAllowed", "true");
        AppendStringIdElement(doc, root, "conditionsCourse", -1, "Invalid", "");
        AppendTextElement(doc, root, "conditionsText", "");
        AppendTextElement(doc, root, "priority", "0");

        AppendElement(doc, root, "infos", infos =>
        {
            foreach (var m in dto.Musics)
            {
                AppendCourseMusicInfo(doc, infos, m);
            }
        });

        doc.Save(Path.Combine(targetDir, "Course.xml"));
        return Ok();
    }

    #endregion

    #region Save

    [HttpPost]
    public ActionResult SaveCourse([FromQuery] int id, [FromQuery] string assetDir, [FromBody] SaveCourseDto dto)
    {
        var xmlPath = FindCourseXml(id, assetDir);
        if (xmlPath == null) return NotFound();

        try
        {
            var doc = new XmlDocument();
            doc.Load(xmlPath);
            var root = doc.SelectSingleNode("/CourseData") as XmlElement;
            if (root == null) return NotFound();

            SetNodeText(root, "name/str", dto.Name);
            SetNodeText(root, "difficulty/id", dto.DifficultyId.ToString());
            SetNodeText(root, "difficulty/str", $"ID_{dto.DifficultyId}");
            SetNodeText(root, "difficulty/data", dto.Difficulty);
            SetNodeText(root, "rule/id", dto.RuleId.ToString());
            SetNodeText(root, "rule/str", $"{dto.RuleId:D4}");
            SetNodeText(root, "reward/id", dto.RewardId.ToString());
            SetNodeText(root, "reward/str", dto.RewardName);
            SetNodeText(root, "reward2nd/id", dto.Reward2ndId.ToString());
            SetNodeText(root, "reward2nd/str", dto.Reward2ndName);
            SetNodeText(root, "teamOnly", dto.TeamOnly.ToString().ToLower());
            SetNodeText(root, "isMusicDuplicateAllowed", dto.IsMusicDuplicateAllowed.ToString().ToLower());
            SetNodeText(root, "conditionsCourse/id", dto.ConditionsCourseId.ToString());
            SetNodeText(root, "conditionsCourse/str", dto.ConditionsCourseName);
            SetNodeText(root, "conditionsText", dto.ConditionsText);
            SetNodeText(root, "priority", dto.Priority.ToString());

            var oldInfos = root.SelectSingleNode("infos");
            if (oldInfos != null)
                root.RemoveChild(oldInfos);

            AppendElement(doc, root, "infos", infos =>
            {
                foreach (var m in dto.Musics)
                {
                    AppendCourseMusicInfo(doc, infos, m);
                }
            });

            doc.Save(xmlPath);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest($"保存失败: {ex.Message}");
        }
    }

    #endregion

    #region Delete

    [HttpPost]
    public ActionResult DeleteCourse([FromQuery] int id, [FromQuery] string assetDir)
    {
        var xmlPath = FindCourseXml(id, assetDir);
        if (xmlPath == null) return NotFound();

        var dir = Path.GetDirectoryName(xmlPath);
        if (dir != null && Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }

        return Ok();
    }

    #endregion

    #region Helpers

    private static IEnumerable<(string courseDir, string assetDir)> GetCourseDirectories(string? source)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            yield break;

        if (source == null || source == "A000")
        {
            var baseCourse = Path.Combine(StaticSettings.GamePath, "data", "A000", "course");
            if (Directory.Exists(baseCourse))
            {
                foreach (var dir in Directory.EnumerateDirectories(baseCourse))
                    yield return (dir, "A000");
            }
        }

        var optionRoot = Path.Combine(StaticSettings.GamePath, "bin", "option");
        if (!Directory.Exists(optionRoot)) yield break;

        foreach (var optDir in Directory.EnumerateDirectories(optionRoot).OrderBy(d => d))
        {
            var dirName = Path.GetFileName(optDir);
            if (source != null && source != dirName) continue;

            var courseDir = Path.Combine(optDir, "course");
            if (!Directory.Exists(courseDir)) continue;

            foreach (var dir in Directory.EnumerateDirectories(courseDir))
                yield return (dir, dirName);
        }
    }

    private static string? FindCourseXml(int id, string assetDir)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return null;

        string courseRoot;
        if (assetDir == "A000")
            courseRoot = Path.Combine(StaticSettings.GamePath, "data", "A000", "course");
        else
            courseRoot = Path.Combine(StaticSettings.GamePath, "bin", "option", assetDir, "course");

        if (!Directory.Exists(courseRoot))
            return null;

        foreach (var dir in Directory.EnumerateDirectories(courseRoot))
        {
            var xmlPath = Path.Combine(dir, "Course.xml");
            if (!System.IO.File.Exists(xmlPath)) continue;

            try
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                var courseId = doc.SelectSingleNode("/CourseData/name/id")?.InnerText;
                if (courseId != null && int.TryParse(courseId, out var cid) && cid == id)
                    return xmlPath;
            }
            catch { }
        }

        return null;
    }

    private static CourseDetail ParseCourseDetail(XmlNode root, string assetDir)
    {
        var detail = new CourseDetail
        {
            Id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var cid) ? cid : 0,
            Name = root.SelectSingleNode("name/str")?.InnerText ?? "",
            DataName = root.SelectSingleNode("dataName")?.InnerText ?? "",
            AssetDir = assetDir,
            DifficultyId = int.TryParse(root.SelectSingleNode("difficulty/id")?.InnerText, out var did) ? did : 0,
            Difficulty = root.SelectSingleNode("difficulty/data")?.InnerText ?? "",
            RuleId = int.TryParse(root.SelectSingleNode("rule/id")?.InnerText, out var rid) ? rid : 0,
            RuleName = root.SelectSingleNode("rule/str")?.InnerText ?? "",
            RewardId = int.TryParse(root.SelectSingleNode("reward/id")?.InnerText, out var rwid) ? rwid : 0,
            RewardName = root.SelectSingleNode("reward/str")?.InnerText ?? "",
            Reward2ndId = int.TryParse(root.SelectSingleNode("reward2nd/id")?.InnerText, out var rw2id) ? rw2id : 0,
            Reward2ndName = root.SelectSingleNode("reward2nd/str")?.InnerText ?? "",
            TeamOnly = bool.TryParse(root.SelectSingleNode("teamOnly")?.InnerText, out var to) && to,
            IsMusicDuplicateAllowed = bool.TryParse(root.SelectSingleNode("isMusicDuplicateAllowed")?.InnerText, out var mda) && mda,
            ConditionsCourseId = int.TryParse(root.SelectSingleNode("conditionsCourse/id")?.InnerText, out var ccid) ? ccid : -1,
            ConditionsCourseName = root.SelectSingleNode("conditionsCourse/str")?.InnerText ?? "",
            ConditionsText = root.SelectSingleNode("conditionsText")?.InnerText ?? "",
            Priority = int.TryParse(root.SelectSingleNode("priority")?.InnerText, out var pri) ? pri : 0,
        };

        var musicNodes = root.SelectNodes("infos/CourseMusicDataInfo");
        if (musicNodes != null)
        {
            foreach (XmlNode mn in musicNodes)
            {
                detail.Musics.Add(new CourseMusicInfo
                {
                    Type = int.TryParse(mn.SelectSingleNode("type")?.InnerText, out var t) ? t : 0,
                    MusicId = int.TryParse(mn.SelectSingleNode("selectMusic/musicName/id")?.InnerText, out var mid) ? mid : 0,
                    MusicName = mn.SelectSingleNode("selectMusic/musicName/str")?.InnerText ?? "",
                    DiffId = int.TryParse(mn.SelectSingleNode("selectMusic/musicDiff/id")?.InnerText, out var mdid) ? mdid : 0,
                    DiffName = mn.SelectSingleNode("selectMusic/musicDiff/data")?.InnerText ?? "",
                });
            }
        }

        return detail;
    }

    private static void AppendCourseMusicInfo(XmlDocument doc, XmlElement parent, CreateCourseMusicDto m)
    {
        AppendElement(doc, parent, "CourseMusicDataInfo", info =>
        {
            AppendTextElement(doc, info, "type", "0");
            AppendElement(doc, info, "selectMusic", sm =>
            {
                AppendStringIdElement(doc, sm, "musicName", m.MusicId, m.MusicName, "");
                AppendStringIdElement(doc, sm, "musicDiff", m.DiffId, m.DiffName, m.DiffData);
            });
            AppendElement(doc, info, "selectLevel", sl =>
            {
                AppendStringIdElement(doc, sl, "fromLevel", -1, "Invalid", "");
            });
            AppendElement(doc, info, "selectMusicList", sml =>
            {
                AppendElement(doc, sml, "musicList", ml =>
                {
                    AppendElement(doc, ml, "list", _ => { });
                });
                AppendTextElement(doc, sml, "panelType", "0");
                AppendTextElement(doc, sml, "isRecordShown", "true");
            });
        });
    }

    private static string? GetOptionRoot(string dirName)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath) || string.IsNullOrWhiteSpace(dirName))
            return null;

        var dataPath = Path.Combine(StaticSettings.GamePath, "data", dirName);
        if (Directory.Exists(dataPath))
            return dataPath;

        var optionPath = Path.Combine(StaticSettings.GamePath, "bin", "option", dirName);
        if (Directory.Exists(optionPath))
            return optionPath;

        Directory.CreateDirectory(optionPath);
        return optionPath;
    }

    private static void SetNodeText(XmlNode parent, string xpath, string value)
    {
        var node = parent.SelectSingleNode(xpath);
        if (node != null)
            node.InnerText = value;
    }

    #endregion

    #region XML Helpers

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
