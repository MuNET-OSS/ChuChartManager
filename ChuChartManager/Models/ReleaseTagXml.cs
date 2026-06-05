using System.Xml;

namespace ChuChartManager.Models;

public class ReleaseTagXml
{
    private readonly XmlDocument xmlDoc;

    public string GamePath { get; }
    public string AssetDir { get; }
    public string FilePath { get; }
    public int Id { get; private set; }
    public string DataName { get; private set; } = "";
    public string VersionStr { get; set; } = "";
    public string TitleName { get; set; } = "";
    public bool IsCustom => AssetDir != "A000";

    private ReleaseTagXml(string gamePath, string assetDir, string filePath, XmlDocument xmlDoc)
    {
        GamePath = gamePath;
        AssetDir = assetDir;
        FilePath = filePath;
        this.xmlDoc = xmlDoc;
        Parse();
    }

    public static ReleaseTagXml CreateNew(string gamePath, string assetDir, int id, string versionStr, string titleName)
    {
        var dir = Path.Combine(gamePath, "bin", "option", assetDir, "releaseTag", $"releaseTag{id:D6}");
        Directory.CreateDirectory(dir);

        var doc = CreateDocument(id, versionStr, titleName);
        var filePath = Path.Combine(dir, "ReleaseTag.xml");
        var releaseTag = new ReleaseTagXml(gamePath, assetDir, filePath, doc);
        releaseTag.Save();
        return releaseTag;
    }

    public static List<ReleaseTagXml> ScanAll(string gamePath)
    {
        var map = new Dictionary<int, ReleaseTagXml>();

        foreach (var item in ScanDirectory(gamePath, "A000", Path.Combine(gamePath, "data", "A000", "releaseTag")))
            map[item.Id] = item;

        var optionRoot = Path.Combine(gamePath, "bin", "option");
        if (Directory.Exists(optionRoot))
        {
            foreach (var optDir in Directory.EnumerateDirectories(optionRoot).OrderBy(d => d))
            {
                var assetDir = Path.GetFileName(optDir);
                foreach (var item in ScanDirectory(gamePath, assetDir, Path.Combine(optDir, "releaseTag")))
                    map[item.Id] = item;
            }
        }

        return map.Values.OrderBy(x => x.Id).ToList();
    }

    private static IEnumerable<ReleaseTagXml> ScanDirectory(string gamePath, string assetDir, string root)
    {
        if (!Directory.Exists(root)) yield break;

        foreach (var dir in Directory.EnumerateDirectories(root).OrderBy(d => d))
        {
            var filePath = Path.Combine(dir, "ReleaseTag.xml");
            if (!File.Exists(filePath)) continue;

            ReleaseTagXml? item = null;
            try
            {
                var doc = new XmlDocument();
                doc.Load(filePath);
                item = new ReleaseTagXml(gamePath, assetDir, filePath, doc);
            }
            catch (Exception ex)
            {
                Log.Error($"加载 ReleaseTag.xml 失败: {filePath}", ex);
            }

            if (item != null) yield return item;
        }
    }

    private static XmlDocument CreateDocument(int id, string versionStr, string titleName)
    {
        var doc = new XmlDocument();
        var data = doc.CreateElement("ReleaseTagData");
        doc.AppendChild(data);

        AppendText(doc, data, "dataName", $"releaseTag{id:D6}");

        var name = doc.CreateElement("name");
        data.AppendChild(name);
        AppendText(doc, name, "id", id.ToString());
        AppendText(doc, name, "str", versionStr);
        name.AppendChild(doc.CreateElement("data"));

        AppendText(doc, data, "titleName", titleName);
        return doc;
    }

    private static void AppendText(XmlDocument doc, XmlNode parent, string name, string value)
    {
        var node = doc.CreateElement(name);
        node.InnerText = value;
        parent.AppendChild(node);
    }

    private void Parse()
    {
        var root = xmlDoc.SelectSingleNode("/ReleaseTagData");
        if (root == null) return;

        DataName = root.SelectSingleNode("dataName")?.InnerText ?? "";
        Id = int.TryParse(root.SelectSingleNode("name/id")?.InnerText, out var id) ? id : -1;
        VersionStr = root.SelectSingleNode("name/str")?.InnerText ?? "";
        TitleName = root.SelectSingleNode("titleName")?.InnerText ?? "";
    }

    public void Save()
    {
        var root = xmlDoc.SelectSingleNode("/ReleaseTagData") ?? throw new InvalidOperationException("ReleaseTagData not found");
        EnsureTextNode(root, "dataName", $"releaseTag{Id:D6}");

        var name = root.SelectSingleNode("name");
        if (name == null)
        {
            name = xmlDoc.CreateElement("name");
            root.AppendChild(name);
        }

        EnsureTextNode(name, "id", Id.ToString());
        EnsureTextNode(name, "str", VersionStr);
        if (name.SelectSingleNode("data") == null)
            name.AppendChild(xmlDoc.CreateElement("data"));
        EnsureTextNode(root, "titleName", TitleName);

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        xmlDoc.Save(FilePath);
    }

    private void EnsureTextNode(XmlNode parent, string name, string value)
    {
        var node = parent.SelectSingleNode(name);
        if (node == null)
        {
            node = xmlDoc.CreateElement(name);
            parent.AppendChild(node);
        }
        node.InnerText = value;
    }

    public void Delete()
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            Directory.Delete(dir, true);
    }
}
