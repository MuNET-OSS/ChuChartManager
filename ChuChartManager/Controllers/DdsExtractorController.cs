using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class DdsExtractorController : ControllerBase
{
    public class ExtractResult
    {
        public string SourceFile { get; set; } = "";
        public int DdsCount { get; set; }
        public string OutputDir { get; set; } = "";
        public List<string> Files { get; set; } = [];
        public string? Error { get; set; }
    }

    public class ExtractDdsRequest
    {
        public string Path { get; set; } = "";
        public string? OutputDir { get; set; }
    }

    [HttpPost]
    public ActionResult<string> OpenFileDialog()
    {
        string? selected = null;
        var thread = new Thread(() =>
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "AFB/SVO 文件|*.afb;*.svo|所有文件|*.*",
                Title = "选择 AFB/SVO 文件",
                Multiselect = false,
            };
            if (dialog.ShowDialog() == DialogResult.OK)
                selected = dialog.FileName;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return Ok(selected ?? "");
    }

    [HttpPost]
    public ActionResult<string> OpenFolderDialog()
    {
        string? selected = null;
        var thread = new Thread(() =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择包含 AFB/SVO 文件的文件夹",
                UseDescriptionForTitle = true,
            };
            if (dialog.ShowDialog() == DialogResult.OK)
                selected = dialog.SelectedPath;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return Ok(selected ?? "");
    }

    [HttpPost]
    public ActionResult<List<ExtractResult>> ExtractDds([FromBody] ExtractDdsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
            return BadRequest("路径不能为空");

        if (!string.IsNullOrWhiteSpace(request.OutputDir))
        {
            var safeOut = PathGuard.EnsureWithin(StaticSettings.GamePath, request.OutputDir);
            if (safeOut == null)
                return BadRequest("输出目录不在游戏目录范围内");
            request.OutputDir = safeOut;
        }

        var results = new List<ExtractResult>();

        if (System.IO.File.Exists(request.Path))
        {
            var safeIn = PathGuard.EnsureWithin(StaticSettings.GamePath, request.Path);
            if (safeIn == null)
                return BadRequest("源文件不在游戏目录范围内");
            results.Add(ExtractFromFile(safeIn, request.OutputDir));
        }
        else if (Directory.Exists(request.Path))
        {
            var safeIn = PathGuard.EnsureWithin(StaticSettings.GamePath, request.Path);
            if (safeIn == null)
                return BadRequest("源目录不在游戏目录范围内");

            var files = Directory.GetFiles(safeIn, "*.afb", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(safeIn, "*.svo", SearchOption.TopDirectoryOnly));

            foreach (var file in files)
            {
                results.Add(ExtractFromFile(file, request.OutputDir));
            }
        }
        else
        {
            return BadRequest("路径不存在");
        }

        return Ok(results);
    }

    private static ExtractResult ExtractFromFile(string filePath, string? outputDirOverride)
    {
        try
        {
            var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            if (ext != ".afb" && ext != ".svo")
                return new ExtractResult { SourceFile = filePath, Error = $"不支持的文件格式: {ext}" };

            var fileData = System.IO.File.ReadAllBytes(filePath);
            var ddsList = DDSExtractor.DdsExtractor.ExtractDdsFiles(fileData, ext == ".afb");
            if (ddsList.Count == 0)
                return new ExtractResult { SourceFile = filePath, Error = "未提取到 DDS" };

            var baseName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            var outputDir = !string.IsNullOrWhiteSpace(outputDirOverride)
                ? System.IO.Path.Combine(outputDirOverride, $"{baseName}_extracted")
                : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filePath)!, $"{baseName}_extracted");

            Directory.CreateDirectory(outputDir);

            var savedFiles = new List<string>();
            for (var i = 0; i < ddsList.Count; i++)
            {
                var outputPath = System.IO.Path.Combine(outputDir, $"{baseName}_{i + 1}.dds");
                System.IO.File.WriteAllBytes(outputPath, ddsList[i]);
                savedFiles.Add(outputPath);
            }

            return new ExtractResult
            {
                SourceFile = filePath,
                DdsCount = ddsList.Count,
                OutputDir = outputDir,
                Files = savedFiles,
            };
        }
        catch (Exception ex)
        {
            return new ExtractResult { SourceFile = filePath, Error = ex.Message };
        }
    }
}
