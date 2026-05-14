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

    public static Config Config { get; set; } = new();
    public static string GamePath { get; set; } = "";
    public static MusicScanner? Scanner { get; set; }
}
