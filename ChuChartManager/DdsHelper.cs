using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ChuChartManager;

public static class DdsHelper
{
    public static void ConvertPngToDds(string pngPath, string ddsPath)
    {
        var encoder = CreateEncoder();
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(pngPath);
        using var fs = File.Create(ddsPath);
        encoder.EncodeToStream(image, fs);
    }

    /// <summary>
    /// 将图片缩放到指定尺寸后转换为 DDS 文件。
    /// </summary>
    public static void ConvertPngToDdsResized(string pngPath, string ddsPath, int width, int height)
    {
        var encoder = CreateEncoder();
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(pngPath);
        image.Mutate(x => x.Resize(width, height));
        using var fs = File.Create(ddsPath);
        encoder.EncodeToStream(image, fs);
    }

    public static byte[] ConvertPngBytesToDds(byte[] pngBytes)
    {
        var encoder = CreateEncoder();
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(pngBytes);
        using var ms = new MemoryStream();
        encoder.EncodeToStream(image, ms);
        return ms.ToArray();
    }

    private static BcEncoder CreateEncoder()
    {
        var encoder = new BcEncoder(CompressionFormat.Bc3);
        encoder.OutputOptions.GenerateMipMaps = false;
        encoder.OutputOptions.Quality = CompressionQuality.BestQuality;
        encoder.OutputOptions.FileFormat = OutputFileFormat.Dds;
        return encoder;
    }
}
