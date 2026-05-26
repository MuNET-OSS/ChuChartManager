using System.Text.Json.Serialization;
using System.Xml;

namespace ChuChartManager.Models;

public enum Difficulty
{
    Basic = 0,
    Advanced = 1,
    Expert = 2,
    Master = 3,
    Ultima = 4,
    WorldsEnd = 5
}

public class FumenData
{
    public Difficulty Difficulty { get; set; }
    public string DifficultyName { get; set; } = "";
    public bool Enable { get; set; }
    public string FilePath { get; set; } = "";
    public int Level { get; set; }
    public int LevelDecimal { get; set; }
    public string NotesDesigner { get; set; } = "";
    public float DefaultBpm { get; set; }

    // 谱面定数
    [JsonIgnore]
    public decimal LevelValue => Level + LevelDecimal / 100m;

    // 等级显示
    public string LevelDisplay
    {
        get
        {
            if (LevelDecimal >= 70) return $"{Level}+";
            return Level.ToString();
        }
    }

    public string LevelDetailDisplay
    {
        get
        {
            if (LevelDecimal == 0) return Level.ToString();
            if (LevelDecimal >= 70) return $"{Level}+";
            return $"{Level}.{LevelDecimal / 10}";
        }
    }

    public static FumenData FromXml(XmlNode node)
    {
        var typeId = int.Parse(node.SelectSingleNode("type/id")?.InnerText ?? "0");
        var typeStr = node.SelectSingleNode("type/str")?.InnerText ?? "";

        return new FumenData
        {
            Difficulty = (Difficulty)typeId,
            DifficultyName = typeStr,
            Enable = bool.TryParse(node.SelectSingleNode("enable")?.InnerText, out var en) && en,
            FilePath = node.SelectSingleNode("file/path")?.InnerText ?? "",
            Level = int.TryParse(node.SelectSingleNode("level")?.InnerText, out var lv) ? lv : 0,
            LevelDecimal = int.TryParse(node.SelectSingleNode("levelDecimal")?.InnerText, out var ld) ? ld : 0,
            NotesDesigner = node.SelectSingleNode("notesDesigner")?.InnerText ?? "",
            DefaultBpm = float.TryParse(node.SelectSingleNode("defaultBpm")?.InnerText, out var bpm) ? bpm : 0f,
        };
    }
}
