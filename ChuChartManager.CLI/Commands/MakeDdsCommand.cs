using System.ComponentModel;
using ChuChartManager;
using ChuChartManager.CLI.Utils;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ChuChartManager.CLI.Commands;

public class MakeDdsCommand : AsyncCommand<MakeDdsCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<sources>")]
        [Description("要转换的源图片文件（支持 png/jpg/bmp/gif/tiff/webp 等）")]
        public string[] Sources { get; set; } = [];

        [CommandOption("-O|--output <PATH>")]
        [Description("输出文件路径（仅单文件时可用，默认与源文件同名 .dds）")]
        public string? Output { get; set; }

        public override ValidationResult Validate()
        {
            if (Sources.Length == 0)
                return ValidationResult.Error("至少需要一个源文件");

            if (Sources.Length > 1 && !string.IsNullOrEmpty(Output))
                return ValidationResult.Error("多文件转换时不能使用 -O 选项");

            foreach (var source in Sources)
            {
                if (!File.Exists(source))
                    return ValidationResult.Error($"源文件不存在: {source}");
            }

            return ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        int done = 0, failed = 0;
        foreach (var source in settings.Sources)
        {
            try
            {
                TerminalProgress.Set(done * 100 / settings.Sources.Length);
                var output = settings.Sources.Length == 1 && !string.IsNullOrEmpty(settings.Output)
                    ? settings.Output
                    : Path.ChangeExtension(source, ".dds");

                await Task.Run(() => DdsHelper.ConvertPngToDds(source, output));
                AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(Path.GetFileName(source))} → {Markup.Escape(output)}");
                done++;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(Path.GetFileName(source))}: {Markup.Escape(ex.Message)}");
                failed++;
            }
        }

        TerminalProgress.Clear();
        AnsiConsole.MarkupLine($"\n[green]完成: {done} 个转换[/]{(failed > 0 ? $", [yellow]{failed} 个失败[/]" : "")}");
        return failed > 0 ? 1 : 0;
    }
}
