using System.Text.Json;

namespace ChuChartManager;

public class Config
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChuChartManager");

    private static readonly string ConfigFilePath = Path.Combine(AppDataDir, "config.json");

    public string GamePath { get; set; } = "";
    public string UmiguriPath { get; set; } = @"D:\Download\Programs\UMIGURI_NEXT";
    public HashSet<string> HistoryPaths { get; set; } = [];
    public string Locale { get; set; } = "";
    public bool IsExport { get; set; }
    public bool UseAuth { get; set; }
    public string AuthUsername { get; set; } = "";
    public string AuthPassword { get; set; } = "";

    public void Save()
    {
        Directory.CreateDirectory(AppDataDir);
        var tmpPath = ConfigFilePath + ".tmp";
        File.WriteAllText(tmpPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmpPath, ConfigFilePath, overwrite: true);
    }

    public static Config Load()
    {
        if (!File.Exists(ConfigFilePath)) return new Config();
        try
        {
            return JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigFilePath)) ?? new Config();
        }
        catch (Exception ex)
        {
            var backupPath = ConfigFilePath + ".bak";
            try
            {
                File.Copy(ConfigFilePath, backupPath, overwrite: true);
            }
            catch { }
            Log.Warn($"配置文件损坏，已备份到 {backupPath}: {ex.Message}");
            return new Config();
        }
    }
}
