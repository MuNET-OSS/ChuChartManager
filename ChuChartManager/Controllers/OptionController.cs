using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class OptionController(MusicScannerService scannerService) : ControllerBase
{
    public record OptionDirInfo(string DirName, int MusicCount, bool IsCustom, string Version);
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
                var optionPath = scanner.OptionPaths.TryGetValue(source, out var discoveredPath)
                    ? discoveredPath
                    : OptionPathResolver.ResolveExisting(StaticSettings.GamePath, source);
                var markPath = optionPath == null ? "" : Path.Combine(optionPath, "CustomChartsMark.txt");
                isCustom = System.IO.File.Exists(markPath);
            }
            var version = ReadOptVersion(source);
            return new OptionDirInfo(source, musicCount, isCustom, version);
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

        var optionPath = OptionPathResolver.ResolveWritePath(StaticSettings.GamePath, dirName);
        var markPath = Path.Combine(optionPath, "CustomChartsMark.txt");

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

        if (OptionPathResolver.ResolveExisting(StaticSettings.GamePath, dirName) != null)
            return BadRequest("目录已存在");

        var target = Path.Combine(StaticSettings.GamePath, "bin", "option", dirName);
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

        var target = OptionPathResolver.ResolveExisting(StaticSettings.GamePath, dirName);

        if (target == null || !Directory.Exists(target))
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

        if (OptionPathResolver.ResolveExisting(StaticSettings.GamePath, dirName) != null)
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
        var optionPath = OptionPathResolver.ResolveExisting(StaticSettings.GamePath, dirName);
        if (optionPath != null) return optionPath;
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

    private static string ReadOptVersion(string dirName)
    {
        var path = ResolveOptRoot(dirName);
        if (path == null) return "";
        var confPath = Path.Combine(path, "data.conf");
        if (!System.IO.File.Exists(confPath)) return "";

        try
        {
            int major = 0, minor = 0, release = 0;
            foreach (var line in System.IO.File.ReadLines(confPath))
            {
                if (line.StartsWith("VerMajor")) major = int.Parse(line.Split('=')[1].Trim());
                if (line.StartsWith("VerMinor")) minor = int.Parse(line.Split('=')[1].Trim());
                if (line.StartsWith("VerRelease")) release = int.Parse(line.Split('=')[1].Trim());
            }
            var version = major > 0 ? $"{major}.{minor:D2}" : "";
            if (version.Length > 0 && release != 0)
                version += "-" + ReleaseToLetters(release);
            return version;
        }
        catch
        {
            return "";
        }
    }

    private static string ReleaseToLetters(int release)
    {
        var result = "";
        while (release > 0)
        {
            release--;
            result = (char)('A' + release % 26) + result;
            release /= 26;
        }
        return result;
    }
}
