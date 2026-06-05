using System.Xml;

namespace ChuChartManager.Models;

public class GenreSortXml
{
    private readonly XmlDocument xmlDoc;

    public string GamePath { get; }
    public string AssetDir { get; }
    public string FilePath => Path.Combine(GamePath, "bin", "option", AssetDir, "music", "GenreSort.xml");

    private GenreSortXml(string gamePath, string assetDir, XmlDocument xmlDoc)
    {
        GamePath = gamePath;
        AssetDir = assetDir;
        this.xmlDoc = xmlDoc;
    }

    public static GenreSortXml LoadOrCreate(string gamePath, string assetDir)
    {
        var path = Path.Combine(gamePath, "bin", "option", assetDir, "music", "GenreSort.xml");
        var doc = new XmlDocument();
        if (File.Exists(path))
        {
            doc.Load(path);
            var existing = new GenreSortXml(gamePath, assetDir, doc);
            if (existing.Entries.Count > 0) return existing;
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }

        doc = CreateBaseDocument(gamePath, path);
        return new GenreSortXml(gamePath, assetDir, doc);
    }

    private static XmlDocument CreateBaseDocument(string gamePath, string targetPath)
    {
        foreach (var path in GetBaseCandidates(gamePath).Where(p => !string.Equals(p, targetPath, StringComparison.OrdinalIgnoreCase)))
        {
            if (!File.Exists(path)) continue;
            try
            {
                var doc = new XmlDocument();
                doc.Load(path);
                if ((doc.SelectNodes("/SerializeSortData/SortList/StringID")?.Count ?? 0) > 0)
                    return doc;
            }
            catch (Exception ex)
            {
                Log.Error($"加载基准 GenreSort.xml 失败: {path}", ex);
            }
        }

        var fallback = new XmlDocument();
        fallback.LoadXml("""
                         <?xml version="1.0" encoding="utf-8"?>
                         <SerializeSortData xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                           <dataName>music</dataName>
                           <SortList>
                             <StringID><id>99</id><str /><data /></StringID>
                             <StringID><id>0</id><str /><data /></StringID>
                             <StringID><id>2</id><str /><data /></StringID>
                             <StringID><id>3</id><str /><data /></StringID>
                             <StringID><id>6</id><str /><data /></StringID>
                             <StringID><id>1</id><str /><data /></StringID>
                             <StringID><id>7</id><str /><data /></StringID>
                             <StringID><id>8</id><str /><data /></StringID>
                             <StringID><id>9</id><str /><data /></StringID>
                             <StringID><id>5</id><str /><data /></StringID>
                             <StringID><id>10</id><str /><data /></StringID>
                           </SortList>
                         </SerializeSortData>
                         """);
        return fallback;
    }

    private static IEnumerable<string> GetBaseCandidates(string gamePath)
    {
        yield return Path.Combine(gamePath, "data", "A000", "music", "GenreSort.xml");
        yield return Path.Combine(gamePath, "bin", "option", "A001", "music", "GenreSort.xml");
    }

    public static List<GenreSortXml> ScanAll(string gamePath)
    {
        var optionRoot = Path.Combine(gamePath, "bin", "option");
        if (!Directory.Exists(optionRoot)) return [];

        var result = new List<GenreSortXml>();
        foreach (var optDir in Directory.EnumerateDirectories(optionRoot).OrderBy(d => d))
        {
            var assetDir = Path.GetFileName(optDir);
            var path = Path.Combine(optDir, "music", "GenreSort.xml");
            if (!File.Exists(path)) continue;
            try
            {
                var doc = new XmlDocument();
                doc.Load(path);
                result.Add(new GenreSortXml(gamePath, assetDir, doc));
            }
            catch (Exception ex)
            {
                Log.Error($"加载 GenreSort.xml 失败: {path}", ex);
            }
        }

        return result;
    }

    public List<(int Id, string Name)> Entries
    {
        get
        {
            var entries = new List<(int Id, string Name)>();
            var nodes = xmlDoc.SelectNodes("/SerializeSortData/SortList/StringID");
            if (nodes == null) return entries;
            foreach (XmlNode node in nodes)
            {
                if (!int.TryParse(node.SelectSingleNode("id")?.InnerText, out var id)) continue;
                entries.Add((id, node.SelectSingleNode("str")?.InnerText ?? ""));
            }
            return entries;
        }
    }

    public List<int> Ids => Entries.Select(e => e.Id).ToList();

    public bool Contains(int id) => Ids.Contains(id);

    public void Add(int id, string name)
    {
        if (Contains(id))
        {
            SetName(id, name);
            return;
        }
        var sortList = xmlDoc.SelectSingleNode("/SerializeSortData/SortList") ?? throw new InvalidOperationException("SortList not found");
        var stringId = xmlDoc.CreateElement("StringID");

        var idNode = xmlDoc.CreateElement("id");
        idNode.InnerText = id.ToString();
        stringId.AppendChild(idNode);

        var strNode = xmlDoc.CreateElement("str");
        strNode.InnerText = name;
        stringId.AppendChild(strNode);
        stringId.AppendChild(xmlDoc.CreateElement("data"));
        sortList.AppendChild(stringId);
    }

    public void SetName(int id, string name)
    {
        var nodes = xmlDoc.SelectNodes("/SerializeSortData/SortList/StringID");
        if (nodes == null) return;
        foreach (XmlNode node in nodes)
        {
            if (!int.TryParse(node.SelectSingleNode("id")?.InnerText, out var current) || current != id) continue;
            var strNode = node.SelectSingleNode("str");
            if (strNode == null)
            {
                strNode = xmlDoc.CreateElement("str");
                node.AppendChild(strNode);
            }
            strNode.InnerText = name;
        }
    }

    public void Remove(int id)
    {
        var nodes = xmlDoc.SelectNodes("/SerializeSortData/SortList/StringID");
        if (nodes == null) return;
        foreach (XmlNode node in nodes)
        {
            if (!int.TryParse(node.SelectSingleNode("id")?.InnerText, out var current) || current != id) continue;
            node.ParentNode?.RemoveChild(node);
            return;
        }
    }

    public void Save() => xmlDoc.Save(FilePath);
}
