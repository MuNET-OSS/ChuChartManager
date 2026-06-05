using System.Text.RegularExpressions;
using ChuChartManager.Models;

namespace ChuChartManager;

public partial class MusicScanner
{
    [GeneratedRegex(@"^[A-Z][A-Za-z0-9_-]*$")]
    private static partial Regex OptionDirRegex();

    private readonly string _gamePath;

    public Dictionary<string, List<MusicXml>> MusicBySource { get; } = [];
    public List<string> AvailableSources { get; } = [];
    public List<string> Errors { get; } = [];

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
            Errors.Add($"option 目录不存在: {optionRoot}");
            return;
        }

        foreach (var optDir in Directory.EnumerateDirectories(optionRoot).OrderBy(d => d))
        {
            var dirName = Path.GetFileName(optDir);
            if (!OptionDirRegex().IsMatch(dirName)) continue;

            var musicDir = Path.Combine(optDir, "music");
            if (Directory.Exists(musicDir))             // 当 music 目录 存在 时
            {
                ScanMusicDirectory(musicDir, dirName);  // 扫描曲目
            }
            else                                        // 当 music 目录 不存在 时
            {
                // 即使没有 music，也注册为空的源
                MusicBySource[dirName] = new List<MusicXml>();
            }
            AvailableSources.Add(dirName);
        }

        var total = MusicBySource.Values.Sum(l => l.Count);
        Log.Info($"扫描完成: {total} 首曲目, {AvailableSources.Count} 个 option, {Errors.Count} 个错误");
    }

    public static Dictionary<int, string> BuildGenreMap(MusicScanner? scanner)
    {
        var map = new Dictionary<int, string>();
        if (scanner != null)
        {
            foreach (var (_, musics) in scanner.MusicBySource)
            {
                foreach (var m in musics)
                {
                    if (m.GenreId < 0 || map.ContainsKey(m.GenreId)) continue;
                    var name = m.Genres.Count > 0 ? m.Genres[0] : "";
                    if (!string.IsNullOrEmpty(name))
                        map[m.GenreId] = name;
                }
            }
        }
        return map;
    }

    private int ScanMusicDirectory(string musicDir, string assetDir)
    {
        var list = new List<MusicXml>();

        try
        {
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
        }
        catch (DirectoryNotFoundException ex)
        {
            Log.Warn($"扫描时目录已被删除: {musicDir}，{ex.Message}");
            Errors.Add($"目录访问失败: {musicDir}");
            return 0;
        }

        if (list.Count > 0)
            MusicBySource[assetDir] = list;

        Log.Debug($"  {assetDir}: {list.Count} 首");
        return list.Count;
    }
}
