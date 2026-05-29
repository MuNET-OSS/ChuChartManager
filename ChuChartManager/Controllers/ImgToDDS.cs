using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class ToolsController : ControllerBase
{
    public class ConvertImageToDdsRequest
    {
        public string SourcePath { get; set; } = "";
        public string? Format { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public bool GenerateMipMaps { get; set; }
    }

    [HttpPost]
    public IActionResult ConvertImageToDds([FromBody] ConvertImageToDdsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourcePath) || !System.IO.File.Exists(request.SourcePath))
            return BadRequest("源文件不存在");

        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(request.SourcePath);

        var width = request.Width ?? image.Width;
        var height = request.Height ?? image.Height;
        if (width != image.Width || height != image.Height)
            image.Mutate(x => x.Resize(width, height));

        var compressionFormat = request.Format?.ToLowerInvariant() switch
        {
            "bc3" => CompressionFormat.Bc3,
            "bc7" => CompressionFormat.Bc7,
            _ => CompressionFormat.Bc1,
        };

        var encoder = new BcEncoder(compressionFormat)
        {
            OutputOptions =
            {
                GenerateMipMaps = request.GenerateMipMaps,
                FileFormat = OutputFileFormat.Dds,
                Quality = CompressionQuality.BestQuality,
            },
        };

        var fileName = System.IO.Path.GetFileNameWithoutExtension(request.SourcePath) + ".dds";

        using var ms = new System.IO.MemoryStream();
        encoder.EncodeToStream(image, ms);

        return File(ms.ToArray(), "application/octet-stream", fileName);
    }
}
