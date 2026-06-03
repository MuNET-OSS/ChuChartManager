using System.Text;
using Microsoft.AspNetCore.Mvc;
using MuConvert.chu;
using MuConvert.utils;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class ConvertController : ControllerBase
{
    public class ConvertRequest
    {
        public string SourceFormat { get; set; } = "";
        public string TargetFormat { get; set; } = "";
        public string Content { get; set; } = "";
    }

    public class ConvertResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public List<string> Alerts { get; set; } = [];
        public string? Error { get; set; }
    }

    private static readonly string[] ValidFormats = ["c2s", "ugc", "sus"];

    [HttpPost]
    public ActionResult<ConvertResult> ConvertChart([FromBody] ConvertRequest req)
    {
        var src = req.SourceFormat.ToLowerInvariant();
        var tgt = req.TargetFormat.ToLowerInvariant();

        if (!ValidFormats.Contains(src))
            return BadRequest($"不支持的源格式: {req.SourceFormat}");
        if (!ValidFormats.Contains(tgt))
            return BadRequest($"不支持的目标格式: {req.TargetFormat}");
        if (src == tgt)
            return BadRequest("源格式和目标格式相同");
        if (string.IsNullOrWhiteSpace(req.Content))
            return BadRequest("谱面内容为空");

        var allAlerts = new List<Alert>();
        try
        {
            var (chart, parseAlerts) = src switch
            {
                "c2s" => new C2sParser().Parse(req.Content),
                "ugc" => new UgcParser().Parse(req.Content),
                "sus" => new SusParser().Parse(req.Content),
                _ => throw new InvalidOperationException(),
            };
            allAlerts.AddRange(parseAlerts);

            var (output, genAlerts) = tgt switch
            {
                "c2s" => new C2sGenerator().Generate(chart),
                "ugc" => new UgcGenerator().Generate(chart),
                "sus" => new SusGenerator().Generate(chart),
                _ => throw new InvalidOperationException(),
            };
            allAlerts.AddRange(genAlerts);

            return Ok(new ConvertResult
            {
                Success = true,
                Output = output,
                Alerts = allAlerts.Select(a => a.ToString()).ToList(),
            });
        }
        catch (ConversionException ex)
        {
            allAlerts.AddRange(ex.Alerts);
            return BadRequest(new ConvertResult
            {
                Success = false,
                Output = "",
                Alerts = allAlerts.Select(a => a.ToString()).ToList(),
                Error = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ConvertResult
            {
                Success = false,
                Output = "",
                Alerts = allAlerts.Select(a => a.ToString()).ToList(),
                Error = ex.Message,
            });
        }
    }

    [HttpPost]
    public ActionResult ConvertFile()
    {
        if (!Request.HasFormContentType || Request.Form.Files.Count == 0)
            return BadRequest("请上传谱面文件");

        var file = Request.Form.Files[0];
        var targetFormat = Request.Form["targetFormat"].ToString().ToLowerInvariant();
        if (string.IsNullOrEmpty(targetFormat))
            return BadRequest("请指定目标格式");

        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        if (!ValidFormats.Contains(ext))
            return BadRequest($"不支持的文件格式: .{ext}");
        if (!ValidFormats.Contains(targetFormat))
            return BadRequest($"不支持的目标格式: {targetFormat}");
        if (ext == targetFormat)
            return BadRequest("源格式和目标格式相同");

        string content;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
        {
            content = reader.ReadToEnd();
        }

        var allAlerts = new List<Alert>();
        try
        {
            var (chart, parseAlerts) = ext switch
            {
                "c2s" => new C2sParser().Parse(content),
                "ugc" => new UgcParser().Parse(content),
                "sus" => new SusParser().Parse(content),
                _ => throw new InvalidOperationException(),
            };
            allAlerts.AddRange(parseAlerts);

            var (output, genAlerts) = targetFormat switch
            {
                "c2s" => new C2sGenerator().Generate(chart),
                "ugc" => new UgcGenerator().Generate(chart),
                "sus" => new SusGenerator().Generate(chart),
                _ => throw new InvalidOperationException(),
            };
            allAlerts.AddRange(genAlerts);

            var outputFileName = Path.GetFileNameWithoutExtension(file.FileName) + $".{targetFormat}";
            var outputBytes = Encoding.UTF8.GetBytes(output);
            return File(outputBytes, "application/octet-stream", outputFileName);
        }
        catch (ConversionException ex)
        {
            allAlerts.AddRange(ex.Alerts);
            var msg = string.Join("\n", allAlerts.Select(a => a.ToString()).Append(ex.Message));
            return BadRequest(msg);
        }
        catch (Exception ex)
        {
            return BadRequest($"转谱失败: {ex.Message}");
        }
    }
}
