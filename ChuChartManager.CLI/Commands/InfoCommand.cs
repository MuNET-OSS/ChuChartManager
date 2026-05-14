using System.ComponentModel;
using ChuChartManager.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ChuChartManager.CLI.Commands;

public class InfoCommand : Command<InfoCommand.Settings>
{
    private static readonly string[] DiffNames = ["BASIC", "ADVANCED", "EXPERT", "MASTER", "ULTIMA", "WORLD'S END"];

    public class Settings : GameSettings
    {
        [CommandOption("-i|--id <ID>")]
        [Description("曲目 ID")]
        public int MusicId { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var scanner = new MusicScanner(settings.GamePath);
        scanner.ScanAll();

        MusicXml? music = null;
        string? foundSource = null;
        foreach (var (src, list) in scanner.MusicBySource)
        {
            music = list.FirstOrDefault(m => m.Id == settings.MusicId);
            if (music != null) { foundSource = src; break; }
        }

        if (music == null)
        {
            AnsiConsole.MarkupLine($"[red]未找到 ID={settings.MusicId} 的曲目[/]");
            var total = scanner.MusicBySource.Values.Sum(l => l.Count);
            AnsiConsole.MarkupLine($"[dim]已扫描 {total} 首曲目，ID 范围请用 list 命令查看[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(music.Name)}[/]");
        AnsiConsole.MarkupLine($"  作者:   {Markup.Escape(music.Artist)}");
        AnsiConsole.MarkupLine($"  ID:     {music.Id:D4}");
        AnsiConsole.MarkupLine($"  Option: {foundSource}");
        AnsiConsole.MarkupLine($"  流派:   {Markup.Escape(string.Join(", ", music.Genres))}");

        var bpm = music.GetBpmFromChart();
        if (bpm > 0)
            AnsiConsole.MarkupLine($"  BPM:    {bpm:F0}");

        var awb = AudioHelper.FindAwbPath(music);
        AnsiConsole.MarkupLine($"  音频:   {(awb != null ? $"[green]✓[/] {awb}" : "[red]✗ 未找到 AWB 文件[/]")}");

        var jacket = music.GetJacketFullPath();
        AnsiConsole.MarkupLine($"  封面:   {(jacket != null ? $"[green]✓[/] {jacket}" : "[red]✗ 未找到封面文件[/]")}");

        AnsiConsole.WriteLine();
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("难度");
        table.AddColumn("等级");
        table.AddColumn("谱师");
        table.AddColumn("启用");

        for (int i = 0; i < 6; i++)
        {
            var f = music.Fumens[i];
            if (f == null)
            {
                table.AddRow(DiffNames[i], "-", "-", "否");
                continue;
            }
            var level = i == 5 && !string.IsNullOrEmpty(music.WorldsEndTag)
                ? music.WorldsEndTag
                : f.LevelDisplay;
            table.AddRow(
                DiffNames[i],
                Markup.Escape(level),
                Markup.Escape(f.NotesDesigner ?? ""),
                f.Enable ? "[green]是[/]" : "否"
            );
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
