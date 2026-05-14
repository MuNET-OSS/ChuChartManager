using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class OptionController(MusicScannerService scannerService) : ControllerBase
{
    public record OptionDirInfo(string DirName, int MusicCount, bool IsCustom);
    public record ConflictEntry(int MusicId, string MusicName, string Dir, string ConflictDir);

    [HttpGet]
    public ActionResult<List<OptionDirInfo>> GetOptionDirs()
    {
        var scanner = scannerService.Scanner;
        if (scanner == null)
            return Ok(new List<OptionDirInfo>());

        var result = scanner.AvailableSources.Select(source =>
        {
            var musicCount = scanner.MusicBySource.TryGetValue(source, out var list) ? list.Count : 0;
            var isCustom = false;
            if (source != "A000")
            {
                var markPath = Path.Combine(StaticSettings.GamePath, "bin", "option", source, "CustomChartsMark.txt");
                isCustom = System.IO.File.Exists(markPath);
            }
            return new OptionDirInfo(source, musicCount, isCustom);
        }).ToList();

        return Ok(result);
    }

    [HttpPost]
    public ActionResult ToggleCustomMark([FromBody] string dirName)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");
        if (dirName == "A000")
            return BadRequest("不能修改基础包标记");

        var optionRoot = Path.Combine(StaticSettings.GamePath, "bin", "option");
        var markPath = Path.Combine(optionRoot, dirName, "CustomChartsMark.txt");

        if (System.IO.File.Exists(markPath))
            System.IO.File.Delete(markPath);
        else
            System.IO.File.WriteAllText(markPath, "Custom charts directory");

        return Ok();
    }

    [HttpGet]
    public ActionResult<List<ConflictEntry>> CheckConflict([FromQuery] string dirName)
    {
        var scanner = scannerService.Scanner;
        if (scanner == null)
            return Ok(new List<ConflictEntry>());

        if (!scanner.MusicBySource.TryGetValue(dirName, out var dirMusic))
            return Ok(new List<ConflictEntry>());

        var conflicts = new List<ConflictEntry>();
        foreach (var music in dirMusic)
        {
            foreach (var (otherSource, otherList) in scanner.MusicBySource)
            {
                if (otherSource == dirName) continue;
                var conflict = otherList.FirstOrDefault(m => m.Id == music.Id);
                if (conflict != null)
                {
                    conflicts.Add(new ConflictEntry(
                        music.Id,
                        music.Name,
                        dirName,
                        otherSource
                    ));
                }
            }
        }

        return Ok(conflicts);
    }

    [HttpPost]
    public ActionResult CreateOptionDir([FromBody] string dirName)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");

        if (string.IsNullOrWhiteSpace(dirName))
            return BadRequest("目录名不能为空");

        var optionRoot = Path.Combine(StaticSettings.GamePath, "bin", "option");
        var target = Path.Combine(optionRoot, dirName);

        if (Directory.Exists(target))
            return BadRequest("目录已存在");

        Directory.CreateDirectory(target);
        Directory.CreateDirectory(Path.Combine(target, "music"));

        var newScanner = new MusicScanner(StaticSettings.GamePath);
        newScanner.ScanAll();
        StaticSettings.Scanner = newScanner;

        return Ok();
    }

    [HttpPost]
    public ActionResult DeleteOptionDir([FromBody] string dirName)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");

        // 不允许删除 A000 基础包
        if (dirName == "A000")
            return BadRequest("不能删除基础包");

        var optionRoot = Path.Combine(StaticSettings.GamePath, "bin", "option");
        var target = Path.Combine(optionRoot, dirName);

        if (!Directory.Exists(target))
            return NotFound("目录不存在");

        Directory.Delete(target, true);

        var newScanner = new MusicScanner(StaticSettings.GamePath);
        newScanner.ScanAll();
        StaticSettings.Scanner = newScanner;

        return Ok();
    }

    [HttpPost]
    public ActionResult ImportLocalOptionDir()
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");

        string? selected = null;
        var thread = new Thread(() =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "请选择资源目录（OPT）\n的文件夹",
                ShowNewFolderButton = false
            };
            if (dialog.ShowDialog() == DialogResult.OK)
                selected = dialog.SelectedPath;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (selected == null)
            return Ok(new { imported = false });

        var dirName = Path.GetFileName(selected);
        var optionRoot = Path.Combine(StaticSettings.GamePath, "bin", "option");
        var dest = Path.Combine(optionRoot, dirName);

        if (Directory.Exists(dest))
        {
            dirName = $"{dirName}_{DateTime.Now:yyyyMMddHHmmss}";
            dest = Path.Combine(optionRoot, dirName);
        }

        CopyDirectory(selected, dest);

        var newScanner = new MusicScanner(StaticSettings.GamePath);
        newScanner.ScanAll();
        StaticSettings.Scanner = newScanner;

        return Ok(new { imported = true, dirName });
    }

    [HttpPost]
    public ActionResult RescanOptions()
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");

        var newScanner = new MusicScanner(StaticSettings.GamePath);
        newScanner.ScanAll();
        StaticSettings.Scanner = newScanner;

        return Ok();
    }

    private static string? ResolveOptRoot(string dirName)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath) || string.IsNullOrWhiteSpace(dirName))
            return null;
        var dataPath = Path.Combine(StaticSettings.GamePath, "data", dirName);
        if (Directory.Exists(dataPath)) return dataPath;
        var optionPath = Path.Combine(StaticSettings.GamePath, "bin", "option", dirName);
        if (Directory.Exists(optionPath)) return optionPath;
        return null;
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            System.IO.File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }
}
