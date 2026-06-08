using System.Runtime.CompilerServices;

namespace ChuChartManager;

public static class StaticSettings
{
    public static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChuChartManager");

    public static readonly string ExeDir = AppContext.BaseDirectory;

#if DEBUG
    public static readonly string Wwwroot = Path.Combine(ProjectDir, "wwwroot");
    private static string ProjectDir => Path.GetDirectoryName(GetThisFilePath())!;
    private static string GetThisFilePath([CallerFilePath] string? path = null) => path!;
#else
    public static readonly string Wwwroot = Path.Combine(ExeDir, "wwwroot");
#endif
    public static void ReadGameVersion()
    {
        try
        {
            var path = Path.Combine(GamePath, "data", "A000", "data.conf");
            if (!File.Exists(path)) return;

            int major = 0, minor = 0, release = 0;
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("VerMajor")) major = int.Parse(line.Split('=')[1].Trim());
                if (line.StartsWith("VerMinor")) minor = int.Parse(line.Split('=')[1].Trim());
                if (line.StartsWith("VerRelease")) release = int.Parse(line.Split('=')[1].Trim());
            }
            GameVersion = minor;
            GameVersionStr = $"{major}.{minor:D2}.{release:D2}";
        }
        catch (Exception ex)
        {
            Log.Error("读取游戏版本号失败", ex);
        }
    }

    public static int GameVersion { get; private set; }
    public static string GameVersionStr { get; private set; } = "";
    public static Config Config { get; set; } = new();
    public static string GamePath { get; set; } = "";
    public static MusicScanner? Scanner { get; set; }
}
