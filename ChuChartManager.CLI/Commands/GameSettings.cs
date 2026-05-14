using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ChuChartManager.CLI.Commands;

public class GameSettings : CommandSettings
{
    [CommandOption("-p|--path <PATH>")]
    [Description("游戏根目录路径")]
    public string GamePath { get; set; } = "";

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(GamePath))
        {
            var config = Config.Load();
            if (!string.IsNullOrEmpty(config.GamePath))
            {
                GamePath = config.GamePath;
                return ValidationResult.Success();
            }
            return ValidationResult.Error("请通过 -p 指定游戏根目录，或先用桌面版设置过路径");
        }

        if (!Directory.Exists(GamePath))
            return ValidationResult.Error($"游戏目录不存在: {GamePath}");

        var dataDir = Path.Combine(GamePath, "data", "A000");
        if (!Directory.Exists(dataDir))
            return ValidationResult.Error($"不是有效的 CHUNITHM 游戏目录（未找到 data/A000）: {GamePath}");

        return ValidationResult.Success();
    }
}
