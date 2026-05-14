using System.ComponentModel;
using ChuChartManager.CLI.Utils;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ChuChartManager.CLI.Commands;

public class ExportMp3Command : AsyncCommand<ExportMp3Command.Settings>
{
    public class Settings : GameSettings
    {
        [CommandOption("-i|--id <ID>")]
        [Description("导出指定 ID 的曲目")]
        [DefaultValue(-1)]
        public int MusicId { get; set; }

        [CommandOption("-a|--all")]
        [Description("导出全部曲目")]
        [DefaultValue(false)]
        public bool All { get; set; }

        [CommandOption("-o|--output <DIR>")]
        [Description("输出目录（默认当前目录）")]
        public string? OutputDir { get; set; }

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful) return baseResult;

            if (MusicId < 0 && !All)
                return ValidationResult.Error("请指定 -i <ID> 导出单曲，或 -a 导出全部");

            return ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var outputDir = settings.OutputDir ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outputDir);

        var scanner = new MusicScanner(settings.GamePath);
        AnsiConsole.MarkupLine("[dim]正在扫描...[/]");
        await Task.Run(scanner.ScanAll);

        var allMusic = scanner.MusicBySource.Values.SelectMany(x => x);
        var targets = settings.All
            ? allMusic.ToList()
            : allMusic.Where(m => m.Id == settings.MusicId).ToList();

        if (targets.Count == 0)
        {
            AnsiConsole.MarkupLine(settings.All
                ? "[red]扫描完成但未找到任何曲目[/]"
                : $"[red]未找到 ID={settings.MusicId} 的曲目[/]");
            return 1;
        }

        int done = 0, failed = 0;
        foreach (var music in targets)
        {
            try
            {
                TerminalProgress.Set(done * 100 / targets.Count);
                var wav = await Task.Run(() => AudioHelper.GetWavFromMusic(music));
                if (wav == null)
                {
                    AnsiConsole.MarkupLine($"[yellow]跳过[/] #{music.Id:D4} {Markup.Escape(music.Name)} — 未找到 AWB 音频文件");
                    failed++;
                    continue;
                }

                var outPath = Path.Combine(outputDir, $"{music.CueFileName}.mp3");
                await Task.Run(() => AudioHelper.ExportMp3(wav, outPath));
                AnsiConsole.MarkupLine($"[green]✓[/] #{music.Id:D4} {Markup.Escape(music.Name)} → {outPath}");
                done++;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] #{music.Id:D4} {Markup.Escape(music.Name)}: {Markup.Escape(ex.Message)}");
                failed++;
            }
        }

        TerminalProgress.Clear();
        AnsiConsole.MarkupLine($"\n[green]完成: {done} 首导出[/]{(failed > 0 ? $", [yellow]{failed} 首失败[/]" : "")}");
        return failed > 0 ? 1 : 0;
    }
}
