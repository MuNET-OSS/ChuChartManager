using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using ChuChartManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class EmoteController(EmoteWebGlService emoteWebGl) : ControllerBase
{
    public class EmoteDataItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string DataName { get; set; } = "";
        public string AssetDir { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
    }

    private static string? SafeGameFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath)) return null;
        return PathGuard.FileExistsWithin(StaticSettings.GamePath, filePath, out var safe) ? safe : null;
    }

    [HttpGet]
    public ActionResult<List<EmoteDataItem>> GetEmoteDataList([FromQuery] string? source = null)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return Ok(new List<EmoteDataItem>());

        var namesBySource = LoadEmoteNames(source);
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
                    Name = FindEmoteName(namesBySource, assetDir, id),
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

    private static Dictionary<string, Dictionary<int, string>> LoadEmoteNames(string? source)
    {
        var namesBySource = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (directory, assetDir) in EnumerateDirs("emoteChara", source))
        {
            var xmlPath = Path.Combine(directory, "EmoteChara.xml");
            if (!System.IO.File.Exists(xmlPath))
                continue;

            try
            {
                using var reader = XmlReader.Create(xmlPath, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                });
                var root = XDocument.Load(reader).Root;
                if (root == null || !int.TryParse(root.Element("emoteDataId")?.Value, out var id))
                    continue;

                var name = root.Element("dialogName")?.Value?.Trim();
                if (string.IsNullOrEmpty(name))
                    name = root.Element("name")?.Element("str")?.Value?.Trim();
                if (string.IsNullOrEmpty(name))
                    continue;

                if (!namesBySource.TryGetValue(assetDir, out var names))
                    namesBySource[assetDir] = names = new Dictionary<int, string>();
                names.TryAdd(id, name);
            }
            catch (XmlException)
            {
            }
            catch (IOException)
            {
            }
        }

        return namesBySource;
    }

    private static string FindEmoteName(
        Dictionary<string, Dictionary<int, string>> namesBySource,
        string assetDir,
        int id)
    {
        if (namesBySource.TryGetValue(assetDir, out var names) && names.TryGetValue(id, out var name))
            return name;
        if (!string.Equals(assetDir, "A000", StringComparison.OrdinalIgnoreCase)
            && namesBySource.TryGetValue("A000", out var baseNames)
            && baseNames.TryGetValue(id, out name))
            return name;
        return "";
    }

    [HttpPost]
    public ActionResult LaunchViewer([FromBody] LaunchViewerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
            return BadRequest("文件路径不能为空");

        if (SafeGameFilePath(request.FilePath) == null)
            return BadRequest("文件不在游戏目录范围内");

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

    /// <summary>
    /// emtbytes → PsbDecompile → PsBuild -p ems → pure.psb (WebGL 可用格式)
    /// </summary>
    [HttpGet]
    public ActionResult GetEmoteWebGLData([FromQuery] string filePath)
    {
        var safePath = SafeGameFilePath(filePath);
        if (safePath == null)
            return NotFound();

        if (!emoteWebGl.TryConvert(safePath, out var data, out var error))
            return BadRequest(error);

        return File(data, "application/octet-stream", Path.GetFileNameWithoutExtension(safePath) + ".pure.psb");
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

        foreach (var (dirName, optDir) in OptionPathResolver.EnumerateOptionDirectories(StaticSettings.GamePath))
        {
            if (source != null && source != "A000" && source != dirName) continue;

            var resDir = Path.Combine(optDir, type);
            if (!Directory.Exists(resDir)) continue;

            foreach (var d in Directory.EnumerateDirectories(resDir))
                yield return (d, dirName);
        }
    }
}
