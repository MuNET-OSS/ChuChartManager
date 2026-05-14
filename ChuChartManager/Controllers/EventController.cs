using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using ImageFormat = System.Drawing.Imaging.ImageFormat;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class EventController : ControllerBase
{
    #region Event substance types

    // type 值 → substances 中实际有效的子节点
    // 0=information, 1=map, 2=music, 3=advertiseMovie, 4=recommendMusic
    // 5=release, 6=course, 7=quest, 8=duel, 9=cmission
    // 10=changeSurfBoardUI, 11=avatarAccessoryGacha, 12=rightsInfo
    // 13=dailyBonusPreset, 14=matchingBonus, 15=unlockChallenge
    // 16=playRewardSet, 17=linkedVerse
    private static readonly Dictionary<int, string> SubstanceTypeNames = new()
    {
        [0] = "information",
        [1] = "map",
        [2] = "music",
        [3] = "advertiseMovie",
        [4] = "recommendMusic",
        [5] = "release",
        [6] = "course",
        [7] = "quest",
        [8] = "duel",
        [9] = "cmission",
        [10] = "changeSurfBoardUI",
        [11] = "avatarAccessoryGacha",
        [12] = "rightsInfo",
        [13] = "dailyBonusPreset",
        [14] = "matchingBonus",
        [15] = "unlockChallenge",
        [16] = "playRewardSet",
        [17] = "linkedVerse",
    };

    #endregion

    #region Event DTOs

    public class EventListItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string AssetDir { get; set; } = "";
        public int SubstanceType { get; set; }
        public string SubstanceTypeName { get; set; } = "";
        public bool AlwaysOpen { get; set; }
        public bool TeamOnly { get; set; }
        public bool IsKop { get; set; }
    }

    public class EventDetail
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string DataName { get; set; } = "";
        public string AssetDir { get; set; } = "";
        public string NetOpenName { get; set; } = "";
        public int NetOpenId { get; set; }
        public string Text { get; set; } = "";
        public int DdsBannerId { get; set; }
        public string DdsBannerName { get; set; } = "";
        public string InformationImagePath { get; set; } = "";
        public int PeriodDispType { get; set; }
        public bool AlwaysOpen { get; set; }
        public bool TeamOnly { get; set; }
        public bool IsKop { get; set; }
        public int Priority { get; set; }
        public int SubstanceType { get; set; }
        public string SubstanceTypeName { get; set; } = "";
        public int FlagValue { get; set; }
        public StringIdRef? MapRef { get; set; }
        public StringIdRef? DailyBonusPresetRef { get; set; }
        public StringIdRef? LinkedVerseRef { get; set; }
        public StringIdRef? CmissionRef { get; set; }
        public StringIdRef? PlayRewardSetRef { get; set; }
        public StringIdRef? UnlockChallengeRef { get; set; }
        public StringIdRef? AvatarAccessoryGachaRef { get; set; }
        public StringIdRef? DuelRef { get; set; }
        public StringIdRef? MatchingBonusRef { get; set; }
    }

    public class StringIdRef
    {
        public int Id { get; set; }
        public string Str { get; set; } = "";
    }

    public class SaveEventDto
    {
        public string Name { get; set; } = "";
        public string Text { get; set; } = "";
        public int PeriodDispType { get; set; }
        public bool AlwaysOpen { get; set; }
        public bool TeamOnly { get; set; }
        public bool IsKop { get; set; }
        public int Priority { get; set; }
        public int SubstanceType { get; set; }
        public int FlagValue { get; set; }
    }

    public class CreateEventDto
    {
        public string TargetDir { get; set; } = "";
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int SubstanceType { get; set; }
    }

    #endregion

    #region Map DTOs

    public class MapListItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string AssetDir { get; set; } = "";
        public int MapType { get; set; }
        public int AreaCount { get; set; }
        public string FilterName { get; set; } = "";
    }

    public class MapDetail
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string DataName { get; set; } = "";
        public string AssetDir { get; set; } = "";
        public bool NetDispPeriod { get; set; }
        public int MapType { get; set; }
        public int HiddenType { get; set; }
        public string UnlockText { get; set; } = "";
        public int MapFilterId { get; set; }
        public string MapFilterName { get; set; } = "";
        public string MapFilterData { get; set; } = "";
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public int StopPageIndex { get; set; }
        public int StopReleaseEventId { get; set; }
        public string StopReleaseEventName { get; set; } = "";
        public int Priority { get; set; }
        public List<MapAreaInfoDto> Areas { get; set; } = [];
    }

    public class MapAreaInfoDto
    {
        public int MapAreaId { get; set; }
        public string MapAreaName { get; set; } = "";
        public int DdsMapId { get; set; }
        public string DdsMapName { get; set; } = "";
        public int MusicId { get; set; }
        public string MusicName { get; set; } = "";
        public int RewardId { get; set; }
        public string RewardName { get; set; } = "";
        public bool IsHard { get; set; }
        public int PageIndex { get; set; }
        public int IndexInPage { get; set; }
        public int RequiredAchievementCount { get; set; }
        public int GaugeId { get; set; }
        public string GaugeName { get; set; } = "";
    }

    public class SaveMapDto
    {
        public string Name { get; set; } = "";
        public bool NetDispPeriod { get; set; }
        public int MapType { get; set; }
        public int HiddenType { get; set; }
        public string UnlockText { get; set; } = "";
        public int Priority { get; set; }
        public List<SaveMapAreaDto> Areas { get; set; } = [];
    }

    public class SaveMapAreaDto
    {
        public int MapAreaId { get; set; }
        public string MapAreaName { get; set; } = "";
        public int DdsMapId { get; set; }
        public string DdsMapName { get; set; } = "";
        public int MusicId { get; set; }
        public string MusicName { get; set; } = "";
        public int RewardId { get; set; }
        public string RewardName { get; set; } = "";
        public bool IsHard { get; set; }
        public int PageIndex { get; set; }
        public int IndexInPage { get; set; }
        public int RequiredAchievementCount { get; set; }
        public int GaugeId { get; set; }
        public string GaugeName { get; set; } = "";
    }

    public class CreateMapDto
    {
        public string TargetDir { get; set; } = "";
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class CreateDdsMapDto
    {
        public string TargetDir { get; set; } = "";
        public int DdsMapId { get; set; }
        public string DdsMapName { get; set; } = "";
        public string ImagePath { get; set; } = "";
    }

    #endregion

    #region Event CRUD

    [HttpGet]
    public ActionResult<List<EventListItem>> GetEventList([FromQuery] string? source = null)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return Ok(new List<EventListItem>());

        var result = new List<EventListItem>();
        foreach (var (dir, assetDir) in EnumerateDirs("event", source))
        {
            var xmlPath = Path.Combine(dir, "Event.xml");
            if (!System.IO.File.Exists(xmlPath)) continue;

            try
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                var root = doc.SelectSingleNode("/EventData");
                if (root == null) continue;

                var substType = int.TryParse(root.SelectSingleNode("substances/type")?.InnerText, out var st) ? st : -1;
                result.Add(new EventListItem
                {
                    Id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var id) ? id : 0,
                    Name = root.SelectSingleNode("name/str")?.InnerText ?? "",
                    AssetDir = assetDir,
                    SubstanceType = substType,
                    SubstanceTypeName = SubstanceTypeNames.GetValueOrDefault(substType, $"unknown({substType})"),
                    AlwaysOpen = bool.TryParse(root.SelectSingleNode("alwaysOpen")?.InnerText, out var ao) && ao,
                    TeamOnly = bool.TryParse(root.SelectSingleNode("teamOnly")?.InnerText, out var to) && to,
                    IsKop = bool.TryParse(root.SelectSingleNode("isKop")?.InnerText, out var ik) && ik,
                });
            }
            catch { }
        }

        return Ok(result.OrderBy(e => e.Id).ToList());
    }

    [HttpGet]
    public ActionResult<EventDetail> GetEvent([FromQuery] int id, [FromQuery] string assetDir)
    {
        var xmlPath = FindXml("event", "Event.xml", "EventData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var doc = new XmlDocument();
        doc.Load(xmlPath);
        var root = doc.SelectSingleNode("/EventData");
        if (root == null) return NotFound();

        var substType = int.TryParse(root.SelectSingleNode("substances/type")?.InnerText, out var st) ? st : -1;
        var substances = root.SelectSingleNode("substances");

        var detail = new EventDetail
        {
            Id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var eid) ? eid : 0,
            Name = root.SelectSingleNode("name/str")?.InnerText ?? "",
            DataName = root.SelectSingleNode("dataName")?.InnerText ?? "",
            AssetDir = assetDir,
            NetOpenId = int.TryParse(root.SelectSingleNode("netOpenName/id")?.InnerText, out var noi) ? noi : 0,
            NetOpenName = root.SelectSingleNode("netOpenName/str")?.InnerText ?? "",
            Text = root.SelectSingleNode("text")?.InnerText ?? "",
            DdsBannerId = int.TryParse(root.SelectSingleNode("ddsBannerName/id")?.InnerText, out var dbi) ? dbi : -1,
            DdsBannerName = root.SelectSingleNode("ddsBannerName/str")?.InnerText ?? "",
            InformationImagePath = root.SelectSingleNode("substances/information/image/path")?.InnerText ?? "",
            PeriodDispType = int.TryParse(root.SelectSingleNode("periodDispType")?.InnerText, out var pdt) ? pdt : 0,
            AlwaysOpen = bool.TryParse(root.SelectSingleNode("alwaysOpen")?.InnerText, out var ao) && ao,
            TeamOnly = bool.TryParse(root.SelectSingleNode("teamOnly")?.InnerText, out var to) && to,
            IsKop = bool.TryParse(root.SelectSingleNode("isKop")?.InnerText, out var ik) && ik,
            Priority = int.TryParse(root.SelectSingleNode("priority")?.InnerText, out var pri) ? pri : 0,
            SubstanceType = substType,
            SubstanceTypeName = SubstanceTypeNames.GetValueOrDefault(substType, $"unknown({substType})"),
            FlagValue = int.TryParse(substances?.SelectSingleNode("flag/value")?.InnerText, out var fv) ? fv : 0,
        };

        if (substances != null)
        {
            detail.MapRef = ReadStringIdRef(substances, "map/mapName");
            detail.DailyBonusPresetRef = ReadStringIdRef(substances, "dailyBonusPreset/dailyBonusPresetName");
            detail.LinkedVerseRef = ReadStringIdRef(substances, "linkedVerse/linkedVerseName");
            detail.CmissionRef = ReadStringIdRef(substances, "cmission/cmissionName");
            detail.PlayRewardSetRef = ReadStringIdRef(substances, "playRewardSet/playRewardSetName");
            detail.UnlockChallengeRef = ReadStringIdRef(substances, "unlockChallenge/unlockChallengeName");
            detail.AvatarAccessoryGachaRef = ReadStringIdRef(substances, "avatarAccessoryGacha/avatarAccessoryGachaName");
            detail.DuelRef = ReadStringIdRef(substances, "duel/duelName");
            detail.MatchingBonusRef = ReadStringIdRef(substances, "matchingBonus/timeTableName");
        }

        return Ok(detail);
    }

    [HttpPost]
    public ActionResult CreateEvent([FromBody] CreateEventDto dto)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("名称不能为空");

        var optionRoot = GetOptionRoot(dto.TargetDir);
        if (optionRoot == null) return BadRequest("Invalid target directory");

        var eventDir = Path.Combine(optionRoot, "event", $"event{dto.Id:D8}");
        if (Directory.Exists(eventDir))
            return BadRequest($"Event {dto.Id} already exists");

        Directory.CreateDirectory(eventDir);

        var doc = CreateXmlDocument("EventData");
        var root = (XmlElement)doc.DocumentElement!;

        AppendTextElement(doc, root, "dataName", $"event{dto.Id:D8}");
        AppendStringIdElement(doc, root, "netOpenName", -1, "Invalid");
        AppendStringIdElement(doc, root, "name", dto.Id, dto.Name);
        AppendTextElement(doc, root, "text", "");
        AppendStringIdElement(doc, root, "ddsBannerName", -1, "Invalid");
        AppendTextElement(doc, root, "periodDispType", "1");
        AppendTextElement(doc, root, "alwaysOpen", "true");
        AppendTextElement(doc, root, "teamOnly", "false");
        AppendTextElement(doc, root, "isKop", "false");
        AppendTextElement(doc, root, "priority", "0");

        AppendElement(doc, root, "substances", subst =>
        {
            AppendTextElement(doc, subst, "type", dto.SubstanceType.ToString());
            AppendElement(doc, subst, "flag", f => AppendTextElement(doc, f, "value", "0"));
            BuildDefaultSubstances(doc, subst);
        });

        doc.Save(Path.Combine(eventDir, "Event.xml"));
        return Ok();
    }

    [HttpPost]
    public ActionResult SaveEvent([FromQuery] int id, [FromQuery] string assetDir, [FromBody] SaveEventDto dto)
    {
        var xmlPath = FindXml("event", "Event.xml", "EventData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var doc = new XmlDocument();
        doc.Load(xmlPath);
        var root = doc.SelectSingleNode("/EventData");
        if (root == null) return NotFound();

        SetNodeText(root, "name/str", dto.Name);
        SetNodeText(root, "text", dto.Text);
        SetNodeText(root, "periodDispType", dto.PeriodDispType.ToString());
        SetNodeText(root, "alwaysOpen", dto.AlwaysOpen.ToString().ToLower());
        SetNodeText(root, "teamOnly", dto.TeamOnly.ToString().ToLower());
        SetNodeText(root, "isKop", dto.IsKop.ToString().ToLower());
        SetNodeText(root, "priority", dto.Priority.ToString());
        SetNodeText(root, "substances/type", dto.SubstanceType.ToString());
        SetNodeText(root, "substances/flag/value", dto.FlagValue.ToString());

        doc.Save(xmlPath);
        return Ok();
    }

    [HttpPost]
    public ActionResult DeleteEvent([FromQuery] int id, [FromQuery] string assetDir)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");

        var xmlPath = FindXml("event", "Event.xml", "EventData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var dir = Path.GetDirectoryName(xmlPath)!;
        Directory.Delete(dir, true);
        return Ok();
    }

    #endregion

    #region Map CRUD

    [HttpGet]
    public ActionResult<List<MapListItem>> GetMapList([FromQuery] string? source = null)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return Ok(new List<MapListItem>());

        var result = new List<MapListItem>();
        foreach (var (dir, assetDir) in EnumerateDirs("map", source))
        {
            var xmlPath = Path.Combine(dir, "Map.xml");
            if (!System.IO.File.Exists(xmlPath)) continue;

            try
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                var root = doc.SelectSingleNode("/MapData");
                if (root == null) continue;

                var areas = root.SelectNodes("infos/MapDataAreaInfo");
                result.Add(new MapListItem
                {
                    Id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var id) ? id : 0,
                    Name = root.SelectSingleNode("name/str")?.InnerText ?? "",
                    AssetDir = assetDir,
                    MapType = int.TryParse(root.SelectSingleNode("mapType")?.InnerText, out var mt) ? mt : 0,
                    AreaCount = areas?.Count ?? 0,
                    FilterName = root.SelectSingleNode("mapFilterID/str")?.InnerText ?? "",
                });
            }
            catch { }
        }

        return Ok(result.OrderBy(m => m.Id).ToList());
    }

    [HttpGet]
    public ActionResult<MapDetail> GetMap([FromQuery] int id, [FromQuery] string assetDir)
    {
        var xmlPath = FindXml("map", "Map.xml", "MapData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var doc = new XmlDocument();
        doc.Load(xmlPath);
        var root = doc.SelectSingleNode("/MapData");
        if (root == null) return NotFound();

        var areas = new List<MapAreaInfoDto>();
        var areaNodes = root.SelectNodes("infos/MapDataAreaInfo");
        if (areaNodes != null)
        {
            foreach (XmlNode area in areaNodes)
            {
                areas.Add(new MapAreaInfoDto
                {
                    MapAreaId = int.TryParse(area.SelectSingleNode("mapAreaName/id")?.InnerText, out var aid) ? aid : 0,
                    MapAreaName = area.SelectSingleNode("mapAreaName/str")?.InnerText ?? "",
                    DdsMapId = int.TryParse(area.SelectSingleNode("ddsMapName/id")?.InnerText, out var dmi) ? dmi : 0,
                    DdsMapName = area.SelectSingleNode("ddsMapName/str")?.InnerText ?? "",
                    MusicId = int.TryParse(area.SelectSingleNode("musicName/id")?.InnerText, out var mi) ? mi : -1,
                    MusicName = area.SelectSingleNode("musicName/str")?.InnerText ?? "",
                    RewardId = int.TryParse(area.SelectSingleNode("rewardName/id")?.InnerText, out var ri) ? ri : -1,
                    RewardName = area.SelectSingleNode("rewardName/str")?.InnerText ?? "",
                    IsHard = bool.TryParse(area.SelectSingleNode("isHard")?.InnerText, out var ih) && ih,
                    PageIndex = int.TryParse(area.SelectSingleNode("pageIndex")?.InnerText, out var pi) ? pi : 0,
                    IndexInPage = int.TryParse(area.SelectSingleNode("indexInPage")?.InnerText, out var iip) ? iip : 0,
                    RequiredAchievementCount = int.TryParse(area.SelectSingleNode("requiredAchievementCount")?.InnerText, out var rac) ? rac : 0,
                    GaugeId = int.TryParse(area.SelectSingleNode("gaugeName/id")?.InnerText, out var gi) ? gi : 0,
                    GaugeName = area.SelectSingleNode("gaugeName/str")?.InnerText ?? "",
                });
            }
        }

        return Ok(new MapDetail
        {
            Id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var mid) ? mid : 0,
            Name = root.SelectSingleNode("name/str")?.InnerText ?? "",
            DataName = root.SelectSingleNode("dataName")?.InnerText ?? "",
            AssetDir = assetDir,
            NetDispPeriod = bool.TryParse(root.SelectSingleNode("netDispPeriod")?.InnerText, out var ndp) && ndp,
            MapType = int.TryParse(root.SelectSingleNode("mapType")?.InnerText, out var mt) ? mt : 0,
            HiddenType = int.TryParse(root.SelectSingleNode("hiddenType")?.InnerText, out var ht) ? ht : 0,
            UnlockText = root.SelectSingleNode("unlockText")?.InnerText ?? "",
            MapFilterId = int.TryParse(root.SelectSingleNode("mapFilterID/id")?.InnerText, out var mfi) ? mfi : 0,
            MapFilterName = root.SelectSingleNode("mapFilterID/str")?.InnerText ?? "",
            MapFilterData = root.SelectSingleNode("mapFilterID/data")?.InnerText ?? "",
            CategoryId = int.TryParse(root.SelectSingleNode("categoryName/id")?.InnerText, out var ci) ? ci : 0,
            CategoryName = root.SelectSingleNode("categoryName/str")?.InnerText ?? "",
            StopPageIndex = int.TryParse(root.SelectSingleNode("stopPageIndex")?.InnerText, out var spi) ? spi : 0,
            StopReleaseEventId = int.TryParse(root.SelectSingleNode("stopReleaseEventName/id")?.InnerText, out var srei) ? srei : -1,
            StopReleaseEventName = root.SelectSingleNode("stopReleaseEventName/str")?.InnerText ?? "",
            Priority = int.TryParse(root.SelectSingleNode("priority")?.InnerText, out var pri) ? pri : 0,
            Areas = areas,
        });
    }

    [HttpPost]
    public ActionResult CreateMap([FromBody] CreateMapDto dto)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("名称不能为空");

        var optionRoot = GetOptionRoot(dto.TargetDir);
        if (optionRoot == null) return BadRequest("Invalid target directory");

        var mapDir = Path.Combine(optionRoot, "map", $"map{dto.Id:D8}");
        if (Directory.Exists(mapDir))
            return BadRequest($"Map {dto.Id} already exists");

        Directory.CreateDirectory(mapDir);

        var doc = CreateXmlDocument("MapData");
        var root = (XmlElement)doc.DocumentElement!;

        AppendTextElement(doc, root, "dataName", $"map{dto.Id:D8}");
        AppendTextElement(doc, root, "netDispPeriod", "false");
        AppendStringIdElement(doc, root, "name", dto.Id, dto.Name);
        AppendTextElement(doc, root, "mapType", "0");
        AppendTextElement(doc, root, "hiddenType", "0");
        AppendTextElement(doc, root, "unlockText", "-");
        AppendStringIdElement(doc, root, "mapFilterID", 0, "Collaboration", "イベント");
        AppendStringIdElement(doc, root, "categoryName", 0, "設定なし");
        AppendStringIdElement(doc, root, "timeTableName", -1, "Invalid");
        AppendTextElement(doc, root, "stopPageIndex", "0");
        AppendStringIdElement(doc, root, "stopReleaseEventName", -1, "Invalid");
        AppendTextElement(doc, root, "priority", "0");
        AppendElement(doc, root, "infos", _ => { });

        doc.Save(Path.Combine(mapDir, "Map.xml"));

        return Ok();
    }

    [HttpPost]
    public ActionResult SaveMap([FromQuery] int id, [FromQuery] string assetDir, [FromBody] SaveMapDto dto)
    {
        var xmlPath = FindXml("map", "Map.xml", "MapData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var doc = new XmlDocument();
        doc.Load(xmlPath);
        var root = doc.SelectSingleNode("/MapData");
        if (root == null) return NotFound();

        SetNodeText(root, "name/str", dto.Name);
        SetNodeText(root, "netDispPeriod", dto.NetDispPeriod.ToString().ToLower());
        SetNodeText(root, "mapType", dto.MapType.ToString());
        SetNodeText(root, "hiddenType", dto.HiddenType.ToString());
        SetNodeText(root, "unlockText", dto.UnlockText);
        SetNodeText(root, "priority", dto.Priority.ToString());

        var infosNode = root.SelectSingleNode("infos");
        if (infosNode != null)
        {
            infosNode.RemoveAll();
            foreach (var area in dto.Areas)
            {
                var areaEl = doc.CreateElement("MapDataAreaInfo");
                AppendStringIdElement(doc, areaEl, "mapAreaName", area.MapAreaId, area.MapAreaName);
                AppendStringIdElement(doc, areaEl, "ddsMapName", area.DdsMapId, area.DdsMapName);
                AppendStringIdElement(doc, areaEl, "musicName", area.MusicId, area.MusicName);
                AppendStringIdElement(doc, areaEl, "rewardName", area.RewardId, area.RewardName);
                AppendTextElement(doc, areaEl, "isHard", area.IsHard.ToString().ToLower());
                AppendTextElement(doc, areaEl, "pageIndex", area.PageIndex.ToString());
                AppendTextElement(doc, areaEl, "indexInPage", area.IndexInPage.ToString());
                AppendTextElement(doc, areaEl, "requiredAchievementCount", area.RequiredAchievementCount.ToString());
                AppendStringIdElement(doc, areaEl, "gaugeName", area.GaugeId, area.GaugeName);
                infosNode.AppendChild(areaEl);
            }
        }

        doc.Save(xmlPath);
        return Ok();
    }

    [HttpPost]
    public ActionResult DeleteMap([FromQuery] int id, [FromQuery] string assetDir)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");

        var xmlPath = FindXml("map", "Map.xml", "MapData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var dir = Path.GetDirectoryName(xmlPath)!;
        Directory.Delete(dir, true);
        return Ok();
    }

    #endregion

    #region DDS Preview

    private static readonly object DdsPreviewLock = new();

    [HttpPost]
    public ActionResult CreateDdsMap([FromBody] CreateDdsMapDto dto)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");
        if (string.IsNullOrWhiteSpace(dto.ImagePath) || !System.IO.File.Exists(dto.ImagePath))
            return BadRequest("图片路径无效");

        var optionRoot = GetOptionRoot(dto.TargetDir);
        if (optionRoot == null) return BadRequest("Invalid target directory");

        var ddsMapDir = Path.Combine(optionRoot, "ddsMap", $"ddsMap{dto.DdsMapId:D8}");
        Directory.CreateDirectory(ddsMapDir);

        var ddsFileName = $"CHU_UI_Map_{dto.DdsMapId:D8}.dds";
        DdsHelper.ConvertPngToDds(dto.ImagePath, Path.Combine(ddsMapDir, ddsFileName));

        var doc = CreateXmlDocument("DDSMapData");
        var root = (XmlElement)doc.DocumentElement!;
        AppendTextElement(doc, root, "dataName", $"ddsMap{dto.DdsMapId:D8}");
        AppendStringIdElement(doc, root, "name", dto.DdsMapId, dto.DdsMapName);
        AppendElement(doc, root, "ddsFile", df => AppendTextElement(doc, df, "path", ddsFileName));
        doc.Save(Path.Combine(ddsMapDir, "DDSMap.xml"));

        return Ok();
    }

    [HttpGet]
    public ActionResult GetDdsMapPreview([FromQuery] int ddsMapId)
    {
        var ddsPath = FindDdsMapFile(ddsMapId);
        if (ddsPath == null) return NotFound();

        var pngData = ConvertDdsToPng(ddsPath);
        if (pngData == null) return NotFound();
        return File(pngData, "image/png");
    }

    [HttpGet]
    public ActionResult GetEventInfoImagePreview([FromQuery] int id, [FromQuery] string assetDir)
    {
        var xmlPath = FindXml("event", "Event.xml", "EventData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var doc = new XmlDocument();
        doc.Load(xmlPath);
        var imgRel = doc.SelectSingleNode("/EventData/substances/information/image/path")?.InnerText?.Trim();
        if (string.IsNullOrEmpty(imgRel)) return NotFound();

        var ddsPath = Path.Combine(Path.GetDirectoryName(xmlPath)!, imgRel);
        if (!System.IO.File.Exists(ddsPath)) return NotFound();

        var pngData = ConvertDdsToPng(ddsPath);
        if (pngData == null) return NotFound();
        return File(pngData, "image/png");
    }

    public class ImportEventInfoImageDto
    {
        public string ImagePath { get; set; } = "";
    }

    [HttpPost]
    public ActionResult ImportEventInfoImage([FromQuery] int id, [FromQuery] string assetDir, [FromBody] ImportEventInfoImageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ImagePath) || !System.IO.File.Exists(dto.ImagePath))
            return BadRequest("图片路径无效");

        var xmlPath = FindXml("event", "Event.xml", "EventData", id, assetDir);
        if (xmlPath == null) return NotFound();

        var eventDir = Path.GetDirectoryName(xmlPath)!;
        var ddsFileName = $"CHU_info_event_{id:D8}.dds";
        DdsHelper.ConvertPngToDds(dto.ImagePath, Path.Combine(eventDir, ddsFileName));

        var doc = new XmlDocument();
        doc.Load(xmlPath);
        var pathNode = doc.SelectSingleNode("/EventData/substances/information/image/path");
        if (pathNode != null)
        {
            pathNode.InnerText = ddsFileName;
            doc.Save(xmlPath);
        }

        return Ok();
    }

    private string? FindDdsMapFile(int ddsMapId)
    {
        var xmlPath = FindXml("ddsMap", "DDSMap.xml", "DDSMapData", ddsMapId, null);
        if (xmlPath == null) return null;
        return FindDdsFileFromXml(xmlPath, "ddsFile/path");
    }

    private static string? FindDdsFileFromXml(string xmlPath, string pathXpath)
    {
        try
        {
            var doc = new XmlDocument();
            doc.Load(xmlPath);
            var ddsFileName = doc.SelectSingleNode($"/*/{pathXpath}")?.InnerText?.Trim();
            if (string.IsNullOrEmpty(ddsFileName)) return null;

            var dir = Path.GetDirectoryName(xmlPath)!;
            var ddsPath = Path.Combine(dir, ddsFileName);
            return System.IO.File.Exists(ddsPath) ? ddsPath : null;
        }
        catch { return null; }
    }

    private static byte[]? ConvertDdsToPng(string ddsPath)
    {
        lock (DdsPreviewLock)
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
            catch { return null; }
        }
    }

    #endregion

    #region Helpers

    private static StringIdRef? ReadStringIdRef(XmlNode parent, string xpath)
    {
        var node = parent.SelectSingleNode(xpath);
        if (node == null) return null;
        var idText = node.SelectSingleNode("id")?.InnerText;
        if (idText == null || !int.TryParse(idText, out var id) || id == -1) return null;
        return new StringIdRef { Id = id, Str = node.SelectSingleNode("str")?.InnerText ?? "" };
    }

    private static void BuildDefaultSubstances(XmlDocument doc, XmlElement subst)
    {
        // information
        AppendElement(doc, subst, "information", info =>
        {
            AppendTextElement(doc, info, "informationType", "0");
            AppendTextElement(doc, info, "informationDispType", "0");
            AppendStringIdElement(doc, info, "mapFilterID", -1, "Invalid");
            AppendElement(doc, info, "courseNames", cn => AppendElement(doc, cn, "list", _ => { }));
            AppendTextElement(doc, info, "text", "");
            AppendElement(doc, info, "image", img => AppendElement(doc, img, "path", _ => { }));
            AppendStringIdElement(doc, info, "movieName", -1, "Invalid");
            AppendElement(doc, info, "presentNames", pn => AppendElement(doc, pn, "list", _ => { }));
        });
        // map
        AppendElement(doc, subst, "map", map =>
        {
            AppendTextElement(doc, map, "tagText", "");
            AppendStringIdElement(doc, map, "mapName", -1, "Invalid");
            AppendElement(doc, map, "musicNames", mn => AppendElement(doc, mn, "list", _ => { }));
        });
        // music
        AppendElement(doc, subst, "music", mus =>
        {
            AppendTextElement(doc, mus, "musicType", "0");
            AppendElement(doc, mus, "musicNames", mn => AppendElement(doc, mn, "list", _ => { }));
        });
        // advertiseMovie
        AppendElement(doc, subst, "advertiseMovie", am =>
        {
            AppendStringIdElement(doc, am, "firstMovieName", -1, "Invalid");
            AppendStringIdElement(doc, am, "secondMovieName", -1, "Invalid");
        });
        // recommendMusic
        AppendElement(doc, subst, "recommendMusic", rm =>
        {
            AppendElement(doc, rm, "musicNames", mn => AppendElement(doc, mn, "list", _ => { }));
        });
        // release
        AppendElement(doc, subst, "release", r => AppendTextElement(doc, r, "value", "0"));
        // course
        AppendElement(doc, subst, "course", c =>
        {
            AppendElement(doc, c, "courseNames", cn => AppendElement(doc, cn, "list", _ => { }));
        });
        // quest
        AppendElement(doc, subst, "quest", q =>
        {
            AppendElement(doc, q, "questNames", qn => AppendElement(doc, qn, "list", _ => { }));
        });
        // duel
        AppendElement(doc, subst, "duel", d => AppendStringIdElement(doc, d, "duelName", -1, "Invalid"));
        // cmission
        AppendElement(doc, subst, "cmission", cm => AppendStringIdElement(doc, cm, "cmissionName", -1, "Invalid"));
        // changeSurfBoardUI
        AppendElement(doc, subst, "changeSurfBoardUI", cs => AppendTextElement(doc, cs, "value", "0"));
        // avatarAccessoryGacha
        AppendElement(doc, subst, "avatarAccessoryGacha", aag =>
            AppendStringIdElement(doc, aag, "avatarAccessoryGachaName", -1, "Invalid"));
        // rightsInfo
        AppendElement(doc, subst, "rightsInfo", ri =>
        {
            AppendElement(doc, ri, "rightsNames", rn => AppendElement(doc, rn, "list", _ => { }));
        });
        // playRewardSet
        AppendElement(doc, subst, "playRewardSet", prs =>
            AppendStringIdElement(doc, prs, "playRewardSetName", -1, "Invalid"));
        // dailyBonusPreset
        AppendElement(doc, subst, "dailyBonusPreset", dbp =>
            AppendStringIdElement(doc, dbp, "dailyBonusPresetName", -1, "Invalid"));
        // matchingBonus
        AppendElement(doc, subst, "matchingBonus", mb =>
            AppendStringIdElement(doc, mb, "timeTableName", -1, "Invalid"));
        // unlockChallenge
        AppendElement(doc, subst, "unlockChallenge", uc =>
            AppendStringIdElement(doc, uc, "unlockChallengeName", -1, "Invalid"));
        // linkedVerse
        AppendElement(doc, subst, "linkedVerse", lv =>
            AppendStringIdElement(doc, lv, "linkedVerseName", -1, "Invalid"));
    }

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

        var optionRoot = Path.Combine(StaticSettings.GamePath, "bin", "option");
        if (!Directory.Exists(optionRoot)) yield break;

        foreach (var optDir in Directory.EnumerateDirectories(optionRoot).OrderBy(d => d))
        {
            var dirName = Path.GetFileName(optDir);
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
                resRoot = Path.Combine(StaticSettings.GamePath, "bin", "option", assetDir, type);

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
        return Path.Combine(StaticSettings.GamePath, "bin", "option", dirName);
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

    private static void AppendStringIdElement(XmlDocument doc, XmlElement parent, string name, int id, string str, string? data = null)
    {
        AppendElement(doc, parent, name, n =>
        {
            AppendTextElement(doc, n, "id", id.ToString());
            AppendTextElement(doc, n, "str", str);
            var dataEl = doc.CreateElement("data");
            if (data != null) dataEl.InnerText = data;
            n.AppendChild(dataEl);
        });
    }

    #endregion
}
