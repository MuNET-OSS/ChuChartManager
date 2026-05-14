using Spectre.Console;
using Spectre.Console.Cli;

namespace ChuChartManager.CLI.Commands;

public class ValidateCommand : AsyncCommand<ValidateCommand.Settings>
{
    public class Settings : GameSettings;

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var scanner = new MusicScanner(settings.GamePath);
        AnsiConsole.MarkupLine("[dim]正在扫描...[/]");
        await Task.Run(scanner.ScanAll);

        if (scanner.Errors.Count > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]扫描时发现 {scanner.Errors.Count} 个解析错误:[/]");
            foreach (var err in scanner.Errors)
                AnsiConsole.MarkupLine($"  [red]•[/] {Markup.Escape(err)}");
            AnsiConsole.WriteLine();
        }

        int total = 0, missingAudio = 0, missingJacket = 0, noDiffs = 0;

        foreach (var list in scanner.MusicBySource.Values)
        {
            foreach (var music in list)
            {
                total++;
                var issues = new List<string>();

                if (AudioHelper.FindAwbPath(music) == null)
                {
                    missingAudio++;
                    issues.Add("[yellow]无音频[/]");
                }

                if (music.GetJacketFullPath() == null)
                {
                    missingJacket++;
                    issues.Add("[yellow]无封面[/]");
                }

                var enabledCount = music.Fumens.Count(f => f is { Enable: true });
                if (enabledCount == 0)
                {
                    noDiffs++;
                    issues.Add("[yellow]无启用难度[/]");
                }

                if (issues.Count > 0)
                    AnsiConsole.MarkupLine($"  #{music.Id:D4} {Markup.Escape(music.Name)}: {string.Join(", ", issues)}");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]检查完成[/]: 共 {total} 首");

        if (missingAudio == 0 && missingJacket == 0 && noDiffs == 0 && scanner.Errors.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]✓ 数据完整，无问题[/]");
            return 0;
        }

        if (missingAudio > 0) AnsiConsole.MarkupLine($"  缺音频: [yellow]{missingAudio}[/]");
        if (missingJacket > 0) AnsiConsole.MarkupLine($"  缺封面: [yellow]{missingJacket}[/]");
        if (noDiffs > 0) AnsiConsole.MarkupLine($"  无难度: [yellow]{noDiffs}[/]");

        return 1;
    }
}
