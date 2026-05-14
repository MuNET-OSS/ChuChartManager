using System.Text.RegularExpressions;
using ChuChartManager.Models;

namespace ChuChartManager;

public partial class MusicScanner
{
    [GeneratedRegex(@"^[A-Z](\w+)$")]
    private static partial Regex OptionDirRegex();

    private readonly string _gamePath;

    public Dictionary<string, List<MusicXml>> MusicBySource { get; } = [];
    public List<string> AvailableSources { get; } = [];
    public List<string> Errors { get; } = [];

    public static readonly Dictionary<int, string> GenreMap = new()
    {
        [0] = "POPS & ANIME",
        [2] = "niconico",
        [3] = "東方Project",
        [5] = "ORIGINAL",
        [6] = "VARIETY",
        [7] = "イロドリミドリ",
        [9] = "ゲキマイ",
    };

    public MusicScanner(string gamePath)
    {
        _gamePath = gamePath;
    }

    public void ScanAll()
    {
        MusicBySource.Clear();
        AvailableSources.Clear();
        Errors.Clear();

        Log.Info($"开始扫描: {_gamePath}");

        var baseMusic = Path.Combine(_gamePath, "data", "A000", "music");
        if (Directory.Exists(baseMusic))
        {
            ScanMusicDirectory(baseMusic, "A000");
            AvailableSources.Add("A000");
        }

        var optionRoot = Path.Combine(_gamePath, "bin", "option");
        if (!Directory.Exists(optionRoot))
        {
            Log.Warn($"option 目录不存在: {optionRoot}");
            return;
        }

        foreach (var optDir in Directory.EnumerateDirectories(optionRoot).OrderBy(d => d))
        {
            var dirName = Path.GetFileName(optDir);
            if (!OptionDirRegex().IsMatch(dirName)) continue;

            var musicDir = Path.Combine(optDir, "music");
            if (!Directory.Exists(musicDir)) continue;

            var count = ScanMusicDirectory(musicDir, dirName);
            AvailableSources.Add(dirName);
        }

        var total = MusicBySource.Values.Sum(l => l.Count);
        Log.Info($"扫描完成: {total} 首曲目, {AvailableSources.Count} 个 option, {Errors.Count} 个错误");
    }

    private int ScanMusicDirectory(string musicDir, string assetDir)
    {
        var list = new List<MusicXml>();

        foreach (var subDir in Directory.EnumerateDirectories(musicDir))
        {
            var xmlPath = Path.Combine(subDir, "Music.xml");
            if (!File.Exists(xmlPath)) continue;

            try
            {
                var music = MusicXml.Load(xmlPath, assetDir, "");
                list.Add(music);
            }
            catch (Exception ex)
            {
                Log.Error($"加载 {xmlPath} 失败", ex);
                Errors.Add($"加载 {xmlPath} 失败: {ex.Message}");
            }
        }

        if (list.Count > 0)
            MusicBySource[assetDir] = list;

        Log.Debug($"  {assetDir}: {list.Count} 首");
        return list.Count;
    }
}
