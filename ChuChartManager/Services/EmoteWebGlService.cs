using System.Diagnostics;

namespace ChuChartManager.Services;

public sealed class EmoteWebGlService
{
    private readonly Lock convertLock = new();
    private readonly Dictionary<CacheKey, byte[]> cache = [];

    public bool TryConvert(string filePath, out byte[] data, out string error)
    {
        data = [];
        error = "";

        var file = new FileInfo(filePath);
        if (!file.Exists)
        {
            error = "E-mote 文件不存在";
            return false;
        }

        var cacheKey = new CacheKey(file.FullName, file.Length, file.LastWriteTimeUtc.Ticks);
        lock (convertLock)
        {
            if (cache.TryGetValue(cacheKey, out var cached))
            {
                data = cached;
                return true;
            }

            var decompilePath = Path.Combine(StaticSettings.ExeDir, "tools", "PsbDecompile.exe");
            var buildPath = Path.Combine(StaticSettings.ExeDir, "tools", "PsBuild.exe");
            if (!File.Exists(decompilePath))
            {
                error = "PsbDecompile.exe 未找到";
                return false;
            }
            if (!File.Exists(buildPath))
            {
                error = "PsBuild.exe 未找到";
                return false;
            }

            var baseName = Path.GetFileNameWithoutExtension(file.FullName);
            var tempDir = Path.Combine(Path.GetTempPath(), "CCM_EmoteWebGL", baseName + "_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);

            try
            {
                var tempEmtbytes = Path.Combine(tempDir, baseName + ".emtbytes");
                File.Copy(file.FullName, tempEmtbytes);

                if (!RunTool(decompilePath, tempDir, [tempEmtbytes], out var exitCode))
                {
                    error = $"PsbDecompile 失败，退出码: {exitCode}";
                    return false;
                }

                var jsonPath = Path.Combine(tempDir, baseName + ".json");
                if (!File.Exists(jsonPath))
                {
                    error = "PsbDecompile 失败：未生成 JSON 文件";
                    return false;
                }

                var jsonContent = File.ReadAllText(jsonPath)
                    .Replace("\"type\": \"DXT5\"", "\"type\": \"RGBA8\"")
                    .Replace("\"type\": \"DXT1\"", "\"type\": \"RGBA8\"");
                File.WriteAllText(jsonPath, jsonContent);

                var outputPath = Path.Combine(tempDir, baseName + ".pure.psb");
                if (!RunTool(buildPath, tempDir, ["-p", "ems", "-o", outputPath, jsonPath], out exitCode))
                {
                    error = $"PsBuild 失败，退出码: {exitCode}";
                    return false;
                }
                if (!File.Exists(outputPath))
                {
                    error = "PsBuild 失败：未生成 pure.psb 文件";
                    return false;
                }

                data = File.ReadAllBytes(outputPath);
                foreach (var staleKey in cache.Keys.Where(key =>
                             string.Equals(key.Path, cacheKey.Path, StringComparison.OrdinalIgnoreCase)).ToList())
                    cache.Remove(staleKey);
                cache[cacheKey] = data;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    private static bool RunTool(string toolPath, string workingDirectory, string[] arguments, out int exitCode)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = toolPath,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            exitCode = -1;
            return false;
        }

        if (!process.WaitForExit(30000))
        {
            try { process.Kill(true); } catch { }
            exitCode = -1;
            return false;
        }

        exitCode = process.ExitCode;
        return exitCode == 0;
    }

    private readonly record struct CacheKey(string Path, long Length, long LastWriteTicks);
}
