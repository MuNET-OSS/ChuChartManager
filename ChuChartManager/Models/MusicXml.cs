using System.Text.Json.Serialization;
using System.Xml;

namespace ChuChartManager.Models;

public class MusicXml
{
    [JsonIgnore] public XmlDocument XmlDoc { get; private set; }
    [JsonIgnore] public string XmlFilePath { get; set; }
    [JsonIgnore] public string MusicDirectory { get; set; }

    public string AssetDir { get; set; } = "";
    public string DataSource { get; set; } = "";

    public int Id { get; set; }
    public string DataName { get; set; } = "";
    public string Name { get; set; } = "";
    public string SortName { get; set; } = "";
    public string Artist { get; set; } = "";
    public List<string> Genres { get; set; } = [];
    public int GenreId { get; set; } = -1;
    public string WorksName { get; set; } = "";
    public string JacketFileName { get; set; } = "";
    public string CueFileName { get; set; } = "";
    public string WorldsEndTag { get; set; } = "";
    public int WorldsEndTagId { get; set; } = -1;
    public bool EnableUltima { get; set; }
    public bool IsWorldsEnd { get; set; }
    public string ReleaseDate { get; set; } = "";
    public int StarDifType { get; set; }
    public int ExType { get; set; }
    public bool Disabled { get; set; }
    public string StageName { get; set; } = "";
    public int Priority { get; set; }

    public FumenData[] Fumens { get; set; } = new FumenData[6];

    private MusicXml(string xmlFilePath)
    {
        XmlFilePath = xmlFilePath;
        MusicDirectory = Path.GetDirectoryName(xmlFilePath) ?? "";
        XmlDoc = new XmlDocument();
        XmlDoc.Load(xmlFilePath);
    }

    public static MusicXml Load(string xmlFilePath, string assetDir, string dataSource)
    {
        var music = new MusicXml(xmlFilePath);
        music.AssetDir = assetDir;
        music.DataSource = dataSource;
        music.Parse();
        return music;
    }

    private void Parse()
    {
        var root = XmlDoc.SelectSingleNode("/MusicData");
        if (root == null) return;

        DataName = root.SelectSingleNode("dataName")?.InnerText ?? "";
        Id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var id) ? id : 0;
        Name = root.SelectSingleNode("name/str")?.InnerText ?? "";
        SortName = root.SelectSingleNode("sortName")?.InnerText ?? "";
        Artist = root.SelectSingleNode("artistName/str")?.InnerText ?? "";
        WorksName = root.SelectSingleNode("worksName/str")?.InnerText ?? "";
        JacketFileName = root.SelectSingleNode("jaketFile/path")?.InnerText ?? "";
        CueFileName = root.SelectSingleNode("cueFileName/str")?.InnerText ?? "";
        StageName = root.SelectSingleNode("stageName/str")?.InnerText ?? "";
        ReleaseDate = root.SelectSingleNode("releaseDate")?.InnerText ?? "";
        Priority = int.TryParse(root.SelectSingleNode("priority")?.InnerText, out var pri) ? pri : 0;
        ExType = int.TryParse(root.SelectSingleNode("exType")?.InnerText, out var ext) ? ext : 0;
        StarDifType = int.TryParse(root.SelectSingleNode("starDifType")?.InnerText, out var sdt) ? sdt : 0;
        EnableUltima = bool.TryParse(root.SelectSingleNode("enableUltima")?.InnerText, out var eu) && eu;
        Disabled = bool.TryParse(root.SelectSingleNode("disableFlag")?.InnerText, out var df) && df;

        WorldsEndTagId = int.TryParse(root.SelectSingleNode("worldsEndTagName/id")?.InnerText, out var weId) ? weId : -1;
        WorldsEndTag = root.SelectSingleNode("worldsEndTagName/str")?.InnerText ?? "";
        IsWorldsEnd = ExType == 2;

        var genreNodes = root.SelectNodes("genreNames/list/StringID");
        if (genreNodes != null)
        {
            foreach (XmlNode g in genreNodes)
            {
                var str = g.SelectSingleNode("str")?.InnerText;
                if (!string.IsNullOrWhiteSpace(str))
                    Genres.Add(str);
                if (GenreId < 0 && int.TryParse(g.SelectSingleNode("id")?.InnerText, out var gid))
                    GenreId = gid;
            }
        }

        var fumenNodes = root.SelectNodes("fumens/MusicFumenData");
        if (fumenNodes != null)
        {
            for (var i = 0; i < Math.Min(fumenNodes.Count, 6); i++)
            {
                Fumens[i] = FumenData.FromXml(fumenNodes[i]!);
                if (string.IsNullOrEmpty(Fumens[i].FilePath)) continue;
                var c2sPath = Path.Combine(MusicDirectory, Fumens[i].FilePath);
                if (!File.Exists(c2sPath)) continue;
                if (string.IsNullOrEmpty(Fumens[i].NotesDesigner))
                    Fumens[i].NotesDesigner = FumenData.ReadCreatorFromC2s(c2sPath);
                Fumens[i].NoteCount = FumenData.CountNotesFromC2s(c2sPath);
            }
        }
    }

    public string? GetJacketFullPath()
    {
        if (string.IsNullOrEmpty(JacketFileName)) return null;
        var path = Path.Combine(MusicDirectory, JacketFileName);
        return File.Exists(path) ? path : null;
    }

    public float GetBpmFromChart()
    {
        var fumen = Fumens.FirstOrDefault(f => f is { Enable: true });
        if (fumen == null || string.IsNullOrEmpty(fumen.FilePath)) return 0;

        var c2sPath = Path.Combine(MusicDirectory, fumen.FilePath);
        if (!File.Exists(c2sPath)) return 0;

        foreach (var line in File.ReadLines(c2sPath))
        {
            if (!line.StartsWith("BPM_DEF")) continue;
            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && float.TryParse(parts[1], out var bpm))
                return bpm;
        }
        return 0;
    }

    public void Save()
    {
        XmlDoc.Save(XmlFilePath);
    }
}
