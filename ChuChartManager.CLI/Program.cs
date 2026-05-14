using System.Text;
using ChuChartManager.CLI.Commands;
using Spectre.Console.Cli;

Console.OutputEncoding = Encoding.UTF8;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("ccm");

    config.AddCommand<ListCommand>("list")
        .WithDescription("列出所有曲目")
        .WithExample("list", "-p", "G:\\")
        .WithExample("list", "-p", "G:\\", "-s", "A000", "-l", "50");

    config.AddCommand<InfoCommand>("info")
        .WithDescription("查看单曲详细信息")
        .WithExample("info", "-p", "G:\\", "-i", "100");

    config.AddCommand<ExportMp3Command>("export-mp3")
        .WithDescription("导出曲目音频为 MP3")
        .WithExample("export-mp3", "-p", "G:\\", "-i", "100")
        .WithExample("export-mp3", "-p", "G:\\", "-a", "-o", "mp3_output");

    config.AddCommand<ExportJacketCommand>("export-jacket")
        .WithDescription("导出曲目封面为 PNG")
        .WithExample("export-jacket", "-p", "G:\\", "-i", "100")
        .WithExample("export-jacket", "-p", "G:\\", "-a", "-o", "jacket_output");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("检查数据完整性（缺音频、缺封面、XML 损坏）")
        .WithExample("validate", "-p", "G:\\");

    config.AddCommand<DebugCommand>("debug")
        .WithDescription("以控制台模式启动主程序，用于查看日志输出");
});

return await app.RunAsync(args);
