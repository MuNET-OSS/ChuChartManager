using ChuChartManager;
using ChuChartManager.CLI.Commands;
using ChuChartManager.CLI.Utils;
using Spectre.Console.Cli;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.CancelKeyPress += (_, _) => TerminalProgress.Clear();

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

    config.AddCommand<MakeAcbCommand>("makeacb")
        .WithDescription("将音频文件转换为 ACB 格式")
        .WithExample("makeacb", "audio.wav")
        .WithExample("makeacb", "audio.mp3", "-O", "output.acb")
        .WithExample("makeacb", "audio1.wav", "audio2.mp3");

    config.AddCommand<MakeDdsCommand>("makedds")
        .WithDescription("将图片文件转换为 DDS 格式")
        .WithExample("makedds", "cover.png")
        .WithExample("makedds", "cover.jpg", "-O", "output.dds")
        .WithExample("makedds", "img1.png", "img2.jpg");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("检查数据完整性（缺音频、缺封面、XML 损坏）")
        .WithExample("validate", "-p", "G:\\");

    config.AddCommand<PreviewCommand>("preview")
        .WithDescription("查看或写入 ACB 试听预览区间")
        .WithExample("preview", "music0820.acb")
        .WithExample("preview", "music0820.acb", "-s", "5000", "-e", "20000");

    config.AddCommand<DebugCommand>("debug")
        .WithDescription("以控制台模式启动主程序，用于查看日志输出");
});

return await app.RunAsync(args);
