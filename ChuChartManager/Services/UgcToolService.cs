using System.Diagnostics;

namespace ChuChartManager.Services;

public class UgcToolService
{
    private readonly string? _toolPath;

    public UgcToolService()
    {
        _toolPath = FindUgcTool();
    }

    public bool IsAvailable => _toolPath != null;

    public string? ConvertSusToUgc(string susPath, string ugcOutputPath, bool pretty = true)
    {
        return RunConvert(susPath, ugcOutputPath, pretty);
    }

    public string? ConvertUgcToMgxc(string ugcPath, string mgxcOutputPath)
    {
        return RunConvert(ugcPath, mgxcOutputPath, false);
    }

    public string? ConvertMgxcToUgc(string mgxcPath, string ugcOutputPath, bool pretty = true)
    {
        return RunConvert(ugcPath: mgxcPath, outputPath: ugcOutputPath, pretty);
    }

    public (bool success, string output) Validate(string ugcPath)
    {
        if (_toolPath == null)
            return (false, "ugctool.exe 未找到");

        var tempPath = Path.Combine(Path.GetTempPath(), $"ugctool_validate_{Guid.NewGuid():N}.ugc");
        try
        {
            var result = RunProcess(_toolPath, $"-q -i \"{ugcPath}\" \"{tempPath}\"");
            return (result.exitCode == 0, result.output);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    public string? PrettyPrint(string ugcPath, string outputPath)
    {
        return RunConvert(ugcPath, outputPath, pretty: true);
    }

    private string? RunConvert(string ugcPath, string outputPath, bool pretty)
    {
        if (_toolPath == null) return null;

        var args = pretty ? $"-p -i \"{ugcPath}\" \"{outputPath}\"" : $"-i \"{ugcPath}\" \"{outputPath}\"";
        var result = RunProcess(_toolPath, args);
        return result.exitCode == 0 && File.Exists(outputPath) ? outputPath : null;
    }

    private static (int exitCode, string output) RunProcess(string exe, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var proc = Process.Start(psi);
        if (proc == null) return (-1, "进程启动失败");

        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(30_000);
        return (proc.ExitCode, stdout + stderr);
    }

    private static string? FindUgcTool()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ugctool.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ugctool.exe"),
        };

        foreach (var path in candidates)
            if (File.Exists(path)) return path;

        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in pathDirs)
        {
            var full = Path.Combine(dir, "ugctool.exe");
            if (File.Exists(full)) return full;
        }

        return null;
    }
}
