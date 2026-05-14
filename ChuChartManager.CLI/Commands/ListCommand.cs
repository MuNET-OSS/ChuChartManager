using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ChuChartManager.CLI.Commands;

public class ListCommand : Command<ListCommand.Settings>
{
    public class Settings : GameSettings
    {
        [CommandOption("-s|--source <SOURCE>")]
        [Description("筛选 option（如 A000）")]
        public string? Source { get; set; }

        [CommandOption("-l|--limit <N>")]
        [Description("最多显示条数（默认全部）")]
        [DefaultValue(0)]
        public int Limit { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var scanner = new MusicScanner(settings.GamePath);
        scanner.ScanAll();

        if (scanner.Errors.Count > 0)
            AnsiConsole.MarkupLine($"[yellow]扫描警告: {scanner.Errors.Count} 个错误[/]");

        if (settings.Source != null && !scanner.MusicBySource.ContainsKey(settings.Source))
        {
            AnsiConsole.MarkupLine($"[red]option 不存在: {settings.Source}[/]");
            AnsiConsole.MarkupLine($"[dim]可用 option: {string.Join(", ", scanner.AvailableSources)}[/]");
            return 1;
        }

        var sources = settings.Source != null
            ? [settings.Source]
            : scanner.AvailableSources;

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("ID");
        table.AddColumn("Option");
        table.AddColumn("歌曲名称");
        table.AddColumn("作者");
        table.AddColumn("难度");

        int count = 0;
        foreach (var src in sources)
        {
            if (!scanner.MusicBySource.TryGetValue(src, out var list)) continue;

            foreach (var m in list.OrderBy(x => x.Id))
            {
                if (settings.Limit > 0 && count >= settings.Limit) break;

                var diffs = string.Join("/", m.Fumens
                    .Select((f, i) => f is { Enable: true } ? f.LevelDisplay : null)
                    .Where(x => x != null));

                table.AddRow(
                    m.Id.ToString("D4"),
                    src,
                    Markup.Escape(m.Name),
                    Markup.Escape(m.Artist),
                    diffs
                );
                count++;
            }

            if (settings.Limit > 0 && count >= settings.Limit) break;
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[dim]共 {count} 首[/]");
        return 0;
    }
}
