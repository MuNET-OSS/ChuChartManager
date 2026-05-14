using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class EmoteController : ControllerBase
{
    public class EmoteDataItem
    {
        public int Id { get; set; }
        public string DataName { get; set; } = "";
        public string AssetDir { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
    }

    [HttpGet]
    public ActionResult<List<EmoteDataItem>> GetEmoteDataList([FromQuery] string? source = null)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return Ok(new List<EmoteDataItem>());

        var result = new List<EmoteDataItem>();
        foreach (var (dir, assetDir) in EnumerateDirs("emotedata", source))
        {
            var dirName = Path.GetFileName(dir);
            foreach (var file in Directory.EnumerateFiles(dir, "*.emtbytes"))
            {
                var fi = new FileInfo(file);
                var idStr = dirName.Replace("emotedata", "");
                var id = int.TryParse(idStr, out var parsed) ? parsed : 0;

                result.Add(new EmoteDataItem
                {
                    Id = id,
                    DataName = dirName,
                    AssetDir = assetDir,
                    FileName = fi.Name,
                    FilePath = file,
                    FileSize = fi.Length,
                });
            }
        }

        return Ok(result.OrderBy(e => e.Id).ToList());
    }

    [HttpPost]
    public ActionResult LaunchViewer([FromBody] LaunchViewerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
            return BadRequest("文件路径不能为空");

        if (!System.IO.File.Exists(request.FilePath))
            return BadRequest("文件不存在");

        var viewerPath = Path.Combine(StaticSettings.ExeDir, "tools", "FreeMoteViewer.exe");
        if (!System.IO.File.Exists(viewerPath))
            return BadRequest("FreeMoteViewer.exe 未找到，请将其放置在 tools 目录下");

        try
        {
            var toolsDir = Path.GetDirectoryName(viewerPath)!;
            Process.Start(new ProcessStartInfo
            {
                FileName = viewerPath,
                Arguments = $"\"{request.FilePath}\"",
                UseShellExecute = true,
                WorkingDirectory = toolsDir,
            });
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest($"启动 Viewer 失败: {ex.Message}");
        }
    }

    public class LaunchViewerRequest
    {
        public string FilePath { get; set; } = "";
    }

    private static readonly Lock WebGLConvertLock = new();
    private static readonly Dictionary<string, byte[]> WebGLCache = new();

    /// <summary>
    /// emtbytes → PsbDecompile → PsBuild -p ems → pure.psb (WebGL 可用格式)
    /// </summary>
    [HttpGet]
    public ActionResult GetEmoteWebGLData([FromQuery] string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
            return NotFound();

        if (WebGLCache.TryGetValue(filePath, out var cached))
            return File(cached, "application/octet-stream", Path.GetFileNameWithoutExtension(filePath) + ".pure.psb");

        var decompilePath = Path.Combine(StaticSettings.ExeDir, "tools", "PsbDecompile.exe");
        var buildPath = Path.Combine(StaticSettings.ExeDir, "tools", "PsBuild.exe");
        if (!System.IO.File.Exists(decompilePath))
            return BadRequest("PsbDecompile.exe 未找到");
        if (!System.IO.File.Exists(buildPath))
            return BadRequest("PsBuild.exe 未找到");

        lock (WebGLConvertLock)
        {
            if (WebGLCache.TryGetValue(filePath, out cached))
                return File(cached, "application/octet-stream", Path.GetFileNameWithoutExtension(filePath) + ".pure.psb");

            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var tempDir = Path.Combine(Path.GetTempPath(), "CCM_EmoteWebGL", baseName + "_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);

            try
            {
                var tempEmtbytes = Path.Combine(tempDir, baseName + ".emtbytes");
                System.IO.File.Copy(filePath, tempEmtbytes);

                var decompileProc = Process.Start(new ProcessStartInfo
                {
                    FileName = decompilePath,
                    Arguments = $"\"{tempEmtbytes}\"",
                    UseShellExecute = false,
                    WorkingDirectory = tempDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });
                decompileProc?.WaitForExit(30000);

                var jsonPath = Path.Combine(tempDir, baseName + ".json");
                if (!System.IO.File.Exists(jsonPath))
                    return BadRequest("PsbDecompile 失败：未生成 JSON 文件");

                var jsonContent = System.IO.File.ReadAllText(jsonPath);
                jsonContent = jsonContent.Replace("\"type\": \"DXT5\"", "\"type\": \"RGBA8\"");
                jsonContent = jsonContent.Replace("\"type\": \"DXT1\"", "\"type\": \"RGBA8\"");
                System.IO.File.WriteAllText(jsonPath, jsonContent);

                var outputPath = Path.Combine(tempDir, baseName + ".pure.psb");
                var buildProc = Process.Start(new ProcessStartInfo
                {
                    FileName = buildPath,
                    Arguments = $"-p ems -o \"{outputPath}\" \"{jsonPath}\"",
                    UseShellExecute = false,
                    WorkingDirectory = tempDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });
                buildProc?.WaitForExit(30000);

                if (!System.IO.File.Exists(outputPath))
                    return BadRequest("PsBuild 失败：未生成 pure.psb 文件");

                var psbData = System.IO.File.ReadAllBytes(outputPath);
                WebGLCache[filePath] = psbData;
                return File(psbData, "application/octet-stream", baseName + ".pure.psb");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    private static IEnumerable<(string dir, string assetDir)> EnumerateDirs(string type, string? source)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath)) yield break;

        if (source == null || source == "A000")
        {
            var baseDir = Path.Combine(StaticSettings.GamePath, "data", "A000", type);
            if (Directory.Exists(baseDir))
                foreach (var d in Directory.EnumerateDirectories(baseDir))
                    yield return (d, "A000");
        }

        var optionRoot = Path.Combine(StaticSettings.GamePath, "bin", "option");
        if (!Directory.Exists(optionRoot)) yield break;

        foreach (var optDir in Directory.EnumerateDirectories(optionRoot).OrderBy(d => d))
        {
            var dirName = Path.GetFileName(optDir);
            if (source != null && source != "A000" && source != dirName) continue;

            var resDir = Path.Combine(optDir, type);
            if (!Directory.Exists(resDir)) continue;

            foreach (var d in Directory.EnumerateDirectories(resDir))
                yield return (d, dirName);
        }
    }
}
