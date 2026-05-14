using ChuChartManager.Models;
using SonicAudioLib.Archives;
using NAudio.Wave;
using NAudio.Lame;
using VGAudio.Codecs.CriHca;
using VGAudio.Containers.Hca;
using VGAudio.Containers.Wave;

namespace ChuChartManager;

public class AudioHelper : IDisposable
{
    private WaveOutEvent? _waveOut;
    private WaveStream? _waveStream;

    /// <summary>播放自然结束时触发（非手动停止）</summary>
    public event EventHandler? PlaybackEnded;

    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
    public bool IsPaused => _waveOut?.PlaybackState == PlaybackState.Paused;
    public bool HasAudio => _waveStream != null;

    public TimeSpan CurrentTime => _waveStream?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan TotalTime => _waveStream?.TotalTime ?? TimeSpan.Zero;

    public float Volume
    {
        get => _waveOut?.Volume ?? 1f;
        set { if (_waveOut != null) _waveOut.Volume = Math.Clamp(value, 0f, 1f); }
    }

    public static string? FindAwbPath(MusicXml music)
    {
        var sourceRoot = Path.GetDirectoryName(Path.GetDirectoryName(music.MusicDirectory));
        if (sourceRoot == null) return null;

        var awbPath = Path.Combine(sourceRoot, "cueFile", $"cueFile{music.Id:D6}", $"{music.CueFileName}.awb");
        return File.Exists(awbPath) ? awbPath : null;
    }

    public static byte[]? ExtractHcaFromAwb(string awbPath)
    {
        var archive = new CriAfs2Archive();
        using var fs = File.OpenRead(awbPath);
        archive.Read(fs);

        var entry = archive.FirstOrDefault();
        if (entry == null) return null;

        var data = new byte[entry.Length];
        fs.Seek(entry.Position, SeekOrigin.Begin);
        fs.ReadExactly(data);
        return data;
    }

    private static readonly VGAudio.Codecs.CriHca.CriHcaKey[] Keys =
    [
        new(32931609366120192),    // CHUNITHM (AC)
        new(33426922444908636),    // CHUNITHM International (AC)
        new(30194896045700459)     // CHUNITHM Chinese Version (AC)
    ];

    public static byte[]? DecodeHcaToWav(byte[] hcaData)
    {
        foreach (var key in Keys)
        {
            try
            {
                var hcaReader = new HcaReader { EncryptionKey = key };
                var audioData = hcaReader.Read(hcaData);
                var waveWriter = new WaveWriter();
                return waveWriter.GetFile(audioData);
            }
            catch (InvalidDataException) { }
        }

        var fallback = new HcaReader();
        var audio = fallback.Read(hcaData);
        return new WaveWriter().GetFile(audio);
    }

    public static byte[]? GetWavFromMusic(MusicXml music)
    {
        var awbPath = FindAwbPath(music);
        if (awbPath == null)
        {
            Log.Warn($"未找到 AWB: #{music.Id:D4} {music.Name}");
            return null;
        }

        Log.Debug($"提取 HCA: {awbPath}");
        var hca = ExtractHcaFromAwb(awbPath);
        if (hca == null)
        {
            Log.Warn($"AWB 提取 HCA 失败: {awbPath}");
            return null;
        }

        Log.Debug($"HCA 解码: {hca.Length} bytes");
        return DecodeHcaToWav(hca);
    }

    public void Play(byte[] wavData)
    {
        Stop();

        Log.Info($"播放音频: {wavData.Length / 1024}KB WAV");
        _waveStream = new WaveFileReader(new MemoryStream(wavData));
        _waveOut = new WaveOutEvent();
        _waveOut.PlaybackStopped += OnPlaybackStopped;
        _waveOut.Init(_waveStream);
        _waveOut.Play();
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Pause() => _waveOut?.Pause();

    public void Resume() => _waveOut?.Play();

    public void TogglePlayPause()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else if (IsPaused)
        {
            Resume();
        }
        else if (_waveStream != null && _waveOut != null)
        {
            _waveStream.Position = 0;
            _waveOut.Play();
        }
    }

    public void Seek(TimeSpan position)
    {
        if (_waveStream != null)
            _waveStream.CurrentTime = position;
    }

    public void Stop()
    {
        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Stop();
            _waveOut.Dispose();
            _waveOut = null;
        }
        _waveStream?.Dispose();
        _waveStream = null;
    }

    public static void ExportMp3(byte[] wavData, string outputPath)
    {
        Log.Info($"导出 MP3: {outputPath}");
        using var wavStream = new WaveFileReader(new MemoryStream(wavData));
        using var mp3Writer = new LameMP3FileWriter(outputPath, wavStream.WaveFormat, LAMEPreset.STANDARD);
        wavStream.CopyTo(mp3Writer);
    }

    public static byte[]? EncodeWavToHca(byte[] wavData)
    {
        var waveReader = new VGAudio.Containers.Wave.WaveReader();
        var audioData = waveReader.Read(wavData);

        var hcaWriter = new HcaWriter();
        return hcaWriter.GetFile(audioData);
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
