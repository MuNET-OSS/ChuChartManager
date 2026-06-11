using System.Buffers.Binary;
using System.Security.Cryptography;
using SonicAudioLib.CriMw;
using VGAudio.Containers.Wave;
using VGAudio.Formats.Pcm16;

namespace ChuChartManager;

public static class AcbPreviewHelper
{
    public record PreviewTime(double StartMs, double EndMs);

    // CRIWARE ADX2 序列命令的固定中段（逆向）：其前 4 字节为 loopStart(ms 大端)，
    // 本标记后 4 字节为 loopEnd，再之后为结束符 0x0FA0；标记前导为 0x03 0xE7 0x04
    private static readonly byte[] Marker = [0x07, 0xD0, 0x04, 0x00, 0x02, 0x00, 0x01, 0x07, 0xD1, 0x04];

    public static PreviewTime? Read(string acbPath)
    {
        if (!File.Exists(acbPath)) return null;

        var bytes = File.ReadAllBytes(acbPath);
        var m = Locate(bytes);
        if (m < 0) return null;

        var start = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(m - 4, 4));
        var end = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(m + 10, 4));
        return new PreviewTime(start, end);
    }

    /// <summary>从 template_preview.acb 整体重建 ACB（对齐 MCM CreateAcbWithPreview），AWB 不动</summary>
    public static void Write(string acbPath, string awbPath, int musicId, uint startMs, uint endMs)
    {
        var awbBytes = File.ReadAllBytes(awbPath);
        var hca = AudioHelper.ExtractHcaFromAwb(awbPath) ?? throw new InvalidDataException("AWB 中未找到 HCA");
        var wav = AudioHelper.DecodeHcaToWav(hca) ?? throw new InvalidDataException("HCA 解码失败");

        var format = new WaveReader().Read(wav).GetFormat<Pcm16Format>();
        var durationMs = (uint)(format.SampleCount * 1000.0 / format.SampleRate);
        if (endMs > durationMs) endMs = durationMs;
        if (startMs >= endMs) throw new ArgumentException("预览起点必须小于终点");

        var templatePath = Path.Combine(StaticSettings.ExeDir, "Resources", "template_preview.acb");
        var acb = LoadTable(File.ReadAllBytes(templatePath));

        acb.Rows[0]["Name"] = $"music{musicId:D4}";

        UpdateSubTable(acb, "CueTable", t => t.Rows[0]["Length"] = durationMs);
        UpdateSubTable(acb, "TrackEventTable", t => t.Rows[1]["Command"] = BuildCommand(startMs, endMs));
        UpdateSubTable(acb, "StreamAwbHash", t =>
        {
            t.Rows[0]["Name"] = $"music{musicId:D4}";
            t.Rows[0]["Hash"] = MD5.HashData(awbBytes);
        });
        UpdateSubTable(acb, "WaveformTable", t =>
        {
            t.Rows[0]["SamplingRate"] = (ushort)format.SampleRate;
            t.Rows[0]["NumSamples"] = (uint)format.SampleCount;
        });
        UpdateSubTable(acb, "StreamAwbAfs2Header", t => t.Rows[0]["Header"] = ExtractAfs2Header(awbBytes));

        acb.WriterSettings = CriTableWriterSettings.Adx2Settings;
        using var fs = File.Create(acbPath);
        acb.Write(fs);
    }

    private static CriTable LoadTable(byte[] bytes)
    {
        var table = new CriTable();
        table.Read(new MemoryStream(bytes));
        return table;
    }

    private static void UpdateSubTable(CriTable acb, string name, Action<CriTable> mutate)
    {
        if (acb.Rows[0][name] is not byte[] subBytes || subBytes.Length == 0)
            throw new InvalidDataException($"模板缺少 {name}");

        var sub = LoadTable(subBytes);
        mutate(sub);
        using var ms = new MemoryStream();
        sub.Write(ms);
        acb.Rows[0][name] = ms.ToArray();
    }

    // AFS2 头长度公式（与 MCM 相同，已对 CHUNITHM AWB 实测验证）：
    // 16 字节固定头 + (id宽+offset宽+length宽) × (文件数+1)
    private static byte[] ExtractAfs2Header(byte[] awbBytes)
    {
        var count = BitConverter.ToInt32(awbBytes, 8) + 1;
        var headSize = 16 + awbBytes[5] * count + awbBytes[6] * count + awbBytes[7] * count;
        return awbBytes[..headSize];
    }

    private static byte[] BuildCommand(uint startMs, uint endMs)
    {
        var cmd = new byte[27];
        cmd[0] = 0x03; cmd[1] = 0xE7; cmd[2] = 0x04;
        BinaryPrimitives.WriteUInt32BigEndian(cmd.AsSpan(3, 4), startMs);
        Marker.CopyTo(cmd.AsSpan(7));
        BinaryPrimitives.WriteUInt32BigEndian(cmd.AsSpan(17, 4), endMs);
        ReadOnlySpan<byte> tail = [0x0F, 0xA0, 0x00, 0x00, 0x00, 0x00];
        tail.CopyTo(cmd.AsSpan(21));
        return cmd;
    }

    private static int Locate(byte[] bytes)
    {
        var idx = IndexOf(bytes, Marker);
        if (idx < 7 || idx + 16 > bytes.Length) return -1;
        if (bytes[idx - 7] != 0x03 || bytes[idx - 6] != 0xE7 || bytes[idx - 5] != 0x04) return -1;
        if (bytes[idx + 14] != 0x0F || bytes[idx + 15] != 0xA0) return -1;
        return idx;
    }

    private static int IndexOf(byte[] hay, byte[] needle)
    {
        for (var i = 0; i <= hay.Length - needle.Length; i++)
        {
            var ok = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (hay[i + j] != needle[j]) { ok = false; break; }
            }
            if (ok) return i;
        }
        return -1;
    }
}

