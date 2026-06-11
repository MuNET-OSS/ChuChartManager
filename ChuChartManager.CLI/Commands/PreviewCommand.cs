using System.ComponentModel;
using System.Text.RegularExpressions;
using ChuChartManager;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ChuChartManager.CLI.Commands;

public partial class PreviewCommand : Command<PreviewCommand.Settings>
{
    [GeneratedRegex(@"(\d+)")]
    private static partial Regex MusicIdRegex();

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<acb>")]
        [Description("ACB 文件路径")]
        public string Acb { get; set; } = "";

        [CommandOption("-s|--start <MS>")]
        [Description("预览起点（毫秒），与 --end 一起使用进入写入模式")]
        public uint? Start { get; set; }

        [CommandOption("-e|--end <MS>")]
        [Description("预览终点（毫秒）")]
        public uint? End { get; set; }

        [CommandOption("--awb <PATH>")]
        [Description("AWB 文件路径（默认取 ACB 同目录同名 .awb）")]
        public string? Awb { get; set; }

        [CommandOption("-i|--id <ID>")]
        [Description("曲目 ID（默认从文件名解析）")]
        public int? Id { get; set; }

        public override ValidationResult Validate()
        {
            if (!File.Exists(Acb))
                return ValidationResult.Error($"ACB 文件不存在: {Acb}");
            if (Start.HasValue != End.HasValue)
                return ValidationResult.Error("--start 和 --end 必须同时指定");
            if (Start.HasValue && Start.Value >= End!.Value)
                return ValidationResult.Error("起点必须小于终点");
            return ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!settings.Start.HasValue)
        {
            var preview = AcbPreviewHelper.Read(settings.Acb);
            if (preview == null)
            {
                AnsiConsole.MarkupLine("[yellow]该 ACB 中没有预览命令标记[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"预览区间: [green]{preview.StartMs}ms ~ {preview.EndMs}ms[/] (时长 {preview.EndMs - preview.StartMs}ms)");
            return 0;
        }

        var awbPath = settings.Awb ?? Path.ChangeExtension(settings.Acb, ".awb");
        if (!File.Exists(awbPath))
        {
            AnsiConsole.MarkupLine($"[red]AWB 文件不存在: {Markup.Escape(awbPath)}[/]");
            return 1;
        }

        var id = settings.Id ?? ParseIdFromFileName(settings.Acb);

        AcbPreviewHelper.Write(settings.Acb, awbPath, id, settings.Start.Value, settings.End!.Value);

        var written = AcbPreviewHelper.Read(settings.Acb);
        if (written == null)
        {
            AnsiConsole.MarkupLine("[red]写入后回读失败[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] 已写入预览区间 {written.StartMs}ms ~ {written.EndMs}ms");
        return 0;
    }

    private static int ParseIdFromFileName(string path)
    {
        var match = MusicIdRegex().Match(Path.GetFileNameWithoutExtension(path));
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }
}
