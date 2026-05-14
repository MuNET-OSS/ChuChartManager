using NAudio.Wave;
using VGAudio.Codecs.CriHca;
using VGAudio.Containers.Hca;
using VGAudio.Containers.Wave;
using Xv2CoreLib.ACB;

namespace ChuChartManager;

public static class AcbHelper
{
    private const ulong ChunithmHcaKey = 32931609366120192;
    private static readonly object AcbLock = new();

    public static void PackMusicAcb(string audioPath, string outputAcbPath)
    {
        var templatePath = Path.Combine(StaticSettings.ExeDir, "Resources", "template_music.acb");
        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"模板 ACB 不存在: {templatePath}");

        var hcaBytes = ConvertToHca(audioPath);

        lock (AcbLock)
        {
            var acb = ACB_File.Load(File.ReadAllBytes(templatePath), null);
            var wrapper = new ACB_Wrapper(acb);
            wrapper.Cues[0].AddTrackToCue(hcaBytes, true, false, EncodeType.HCA);
            wrapper.AcbFile.Save(outputAcbPath);
        }
    }

    public static byte[] ConvertToHca(string audioPath)
    {
        var wavBytes = ConvertToWav(audioPath);
        return EncodeWavToHca(wavBytes);
    }

    public static byte[] EncodeWavToHca(byte[] wavData)
    {
        var waveReader = new WaveReader();
        var audioData = waveReader.Read(wavData);

        var hcaWriter = new HcaWriter
        {
            Configuration = new HcaConfiguration
            {
                EncryptionKey = new CriHcaKey(ChunithmHcaKey),
            }
        };
        return hcaWriter.GetFile(audioData);
    }

    private static byte[] ConvertToWav(string audioPath)
    {
        var ext = Path.GetExtension(audioPath).ToLowerInvariant();
        switch (ext)
        {
            case ".wav":
                return File.ReadAllBytes(audioPath);
            case ".mp3":
            case ".ogg":
            case ".wma":
            case ".aac":
                return DecodeToWav(audioPath, ext);
            case ".hca":
                var hcaData = File.ReadAllBytes(audioPath);
                var wavData = AudioHelper.DecodeHcaToWav(hcaData);
                if (wavData == null)
                    throw new InvalidDataException($"HCA 解码失败: {audioPath}");
                return wavData;
            default:
                throw new NotSupportedException($"不支持的音频格式: {ext}");
        }
    }

    private static byte[] DecodeToWav(string audioPath, string ext)
    {
        using WaveStream reader = ext switch
        {
            ".mp3" => new Mp3FileReader(audioPath),
            _ => new MediaFoundationReader(audioPath),
        };

        using var ms = new MemoryStream();
        WaveFileWriter.WriteWavFileToStream(ms, reader.ToSampleProvider().ToWaveProvider16());
        return ms.ToArray();
    }
}
