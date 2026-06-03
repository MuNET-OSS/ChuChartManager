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
    public int NoteCount { get; set; }

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
        var typeId = int.TryParse(node.SelectSingleNode("type/id")?.InnerText, out var tid) ? tid : 0;
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

    public static string ReadCreatorFromC2s(string c2sPath)
    {
        foreach (var line in File.ReadLines(c2sPath))
        {
            if (!line.StartsWith("CREATOR")) continue;
            var parts = line.Split('\t', 2);
            return parts.Length >= 2 ? parts[1].Trim() : "";
        }
        return "";
    }

    private static readonly HashSet<string> NoteTypes = new(StringComparer.Ordinal)
    {
        "TAP", "CHR", "HLD", "SLD", "SLC", "FLK",
        "AIR", "AUR", "AUL", "AHD", "ADW", "ADL", "ADR", "MNE"
    };

    public static int CountNotesFromC2s(string c2sPath)
    {
        var count = 0;
        foreach (var line in File.ReadLines(c2sPath))
        {
            var tab = line.IndexOf('\t');
            if (tab <= 0) continue;
            if (NoteTypes.Contains(line[..tab]))
                count++;
        }
        return count;
    }
}
