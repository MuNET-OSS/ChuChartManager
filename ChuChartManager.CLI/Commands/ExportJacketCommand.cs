using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ChuChartManager.CLI.Utils;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ChuChartManager.CLI.Commands;

public class ExportJacketCommand : AsyncCommand<ExportJacketCommand.Settings>
{
    public class Settings : GameSettings
    {
        [CommandOption("-i|--id <ID>")]
        [Description("导出指定 ID 的封面")]
        [DefaultValue(-1)]
        public int MusicId { get; set; }

        [CommandOption("-a|--all")]
        [Description("导出全部封面")]
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
                return ValidationResult.Error("请指定 -i <ID> 导出单曲封面，或 -a 导出全部");

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
                var jacketPath = music.GetJacketFullPath();
                if (jacketPath == null)
                {
                    AnsiConsole.MarkupLine($"[yellow]跳过[/] #{music.Id:D4} {Markup.Escape(music.Name)} — 未找到封面文件");
                    failed++;
                    continue;
                }

                var outPath = Path.Combine(outputDir, $"{music.Id:D4}.png");
                await Task.Run(() => ConvertToPng(jacketPath, outPath));
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
        AnsiConsole.MarkupLine($"\n[green]完成: {done} 张导出[/]{(failed > 0 ? $", [yellow]{failed} 张失败[/]" : "")}");
        return failed > 0 ? 1 : 0;
    }

    private static void ConvertToPng(string ddsPath, string outPath)
    {
        BitmapSource bmp;
        if (ddsPath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
        {
            using var pfim = Pfim.Pfimage.FromFile(ddsPath);
            if (pfim.Compressed) pfim.Decompress();
            var fmt = pfim.Format == Pfim.ImageFormat.Rgba32
                ? PixelFormats.Bgra32 : PixelFormats.Bgr24;
            bmp = BitmapSource.Create(
                pfim.Width, pfim.Height, 96, 96, fmt, null,
                pfim.Data, pfim.Stride);
        }
        else
        {
            var img = new BitmapImage(new Uri(Path.GetFullPath(ddsPath)));
            img.Freeze();
            bmp = img;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(outPath);
        encoder.Save(fs);
    }
}
