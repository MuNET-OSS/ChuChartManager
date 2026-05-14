using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ChuChartManager.Models;

namespace ChuChartManager.Services;

public enum VirtualKey : ushort
{
    F3 = 0x72,
    F5 = 0x74,
    F9 = 0x78,
    F4 = 0x73,
    Space = 0x20,
    Left = 0x25,
    Right = 0x27
}

public class UmiguriService
{
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint INPUT_KEYBOARD = 1;

    private readonly string _umiguriPath;

    public string UmiguriPath => _umiguriPath;

    public UmiguriService(string? umiguriPath = null)
    {
        _umiguriPath = string.IsNullOrWhiteSpace(umiguriPath)
            ? @"D:\Download\Programs\UMIGURI_NEXT"
            : umiguriPath;
    }

    public string WriteChart(C2sChart chart, UgcConvertOptions options)
    {
        var ugcText = UgcConverter.Convert(chart, options);
        var diffName = GetDifficultyFileName(options.Difficulty);
        var songId = string.IsNullOrWhiteSpace(options.SongId) ? "unknown" : options.SongId;

        var outDir = Path.Combine(UmiguriPath, "data", "music", "ChuChartManager", songId);
        Directory.CreateDirectory(outDir);

        var outPath = Path.Combine(outDir, $"{diffName}.ugc");
        File.WriteAllText(outPath, ugcText);
        return outPath;
    }

    public void PrepareAssets(string musicId, string gameDataRoot, UgcConvertOptions options)
    {
        var songId = string.IsNullOrWhiteSpace(options.SongId) ? "unknown" : options.SongId;
        var outDir = Path.Combine(UmiguriPath, "data", "music", "ChuChartManager", songId);
        Directory.CreateDirectory(outDir);

        ConvertJacket(musicId, gameDataRoot, outDir, options);
        ConvertAudio(musicId, gameDataRoot, outDir, options);
    }

    private static void ConvertJacket(string musicId, string gameDataRoot, string outDir, UgcConvertOptions options)
    {
        var ddsPath = Path.Combine(gameDataRoot, "music", $"music{musicId}", $"CHU_UI_Jacket_{musicId}.dds");
        var pngPath = Path.Combine(outDir, "j.png");

        if (File.Exists(pngPath) || !File.Exists(ddsPath))
            return;

        RunProcess("ffmpeg", $"-i \"{ddsPath}\" -update 1 -frames:v 1 \"{pngPath}\" -y");
        if (File.Exists(pngPath))
            options.JacketFileName = "j.png";
    }

    private static void ConvertAudio(string musicId, string gameDataRoot, string outDir, UgcConvertOptions options)
    {
        var oggPath = Path.Combine(outDir, "bgm.ogg");
        if (File.Exists(oggPath))
        {
            options.BgmFileName = "bgm.ogg";
            return;
        }

        var awbPath = Path.Combine(gameDataRoot, "cueFile", $"cueFile{musicId.PadLeft(6, '0')}", $"music{musicId}.awb");
        if (!File.Exists(awbPath))
            return;

        var wavPath = Path.Combine(outDir, "bgm.wav");
        var vgmstream = FindTool("vgmstream-cli");
        if (vgmstream == null) return;

        RunProcess(vgmstream, $"-o \"{wavPath}\" \"{awbPath}\"");
        if (!File.Exists(wavPath)) return;

        RunProcess("ffmpeg", $"-i \"{wavPath}\" -c:a libvorbis -q:a 6 \"{oggPath}\" -y");
        try { File.Delete(wavPath); } catch { }

        if (File.Exists(oggPath))
            options.BgmFileName = "bgm.ogg";
    }

    private static string? FindTool(string name)
    {
        var exeName = name + ".exe";
        var appDir = AppDomain.CurrentDomain.BaseDirectory;

        var local = Path.Combine(appDir, exeName);
        if (File.Exists(local)) return local;

        var toolsDir = Path.Combine(appDir, "tools", exeName);
        if (File.Exists(toolsDir)) return toolsDir;

        var inPath = FindInPath(exeName);
        if (inPath != null) return inPath;

        return null;
    }

    private static string? FindInPath(string exeName)
    {
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in pathDirs)
        {
            var full = Path.Combine(dir, exeName);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private static void RunProcess(string exe, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit(30_000);
    }

    public bool LaunchUmiguri()
    {
        var exePath = Path.Combine(UmiguriPath, "UMIGURI.exe");
        if (!File.Exists(exePath))
            throw new FileNotFoundException($"找不到 UMIGURI 启动器：{exePath}", exePath);

        if (FindUmiguriWindow() != IntPtr.Zero)
            return false;

        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = UmiguriPath,
            UseShellExecute = true
        });
        return true;
    }

    public bool SendKey(VirtualKey key)
    {
        var hwnd = FindUmiguriWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        SetForegroundWindow(hwnd);

        var inputs = new[]
        {
            CreateKeyInput(key, false),
            CreateKeyInput(key, true)
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        return sent == inputs.Length;
    }

    public bool ReloadChart() => SendKey(VirtualKey.F5);
    public bool ToggleAutoPlay() => SendKey(VirtualKey.F3);
    public bool PlayPause() => SendKey(VirtualKey.Space);

    public IntPtr FindUmiguriWindow()
    {
        var hwnd = FindWindow(null, "UMIGURI");
        if (hwnd != IntPtr.Zero)
            return hwnd;

        hwnd = FindWindow(null, "UMIGURI NEXT");
        if (hwnd != IntPtr.Zero)
            return hwnd;

        hwnd = FindWindow("UnityWndClass", null);
        if (hwnd != IntPtr.Zero)
            return hwnd;

        IntPtr found = IntPtr.Zero;
        EnumWindows((windowHandle, _) =>
        {
            if (!IsWindowVisible(windowHandle))
                return true;

            var title = GetWindowTitle(windowHandle);
            if (title.Contains("UMIGURI", StringComparison.OrdinalIgnoreCase))
            {
                found = windowHandle;
                return false;
            }

            var className = GetWindowClass(windowHandle);
            if (className.Equals("UnityWndClass", StringComparison.Ordinal))
            {
                found = windowHandle;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    public bool IsInstalled() => File.Exists(Path.Combine(UmiguriPath, "UMIGURI.exe"));

    private static string GetDifficultyFileName(int difficulty)
        => difficulty switch
        {
            0 => "bas",
            1 => "adv",
            2 => "exp",
            3 => "mas",
            4 => "ult",
            _ => "mas"
        };

    private static INPUT CreateKeyInput(VirtualKey key, bool keyUp)
        => new()
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)key,
                    wScan = 0,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
                    dwExtraInfo = IntPtr.Zero,
                    time = 0
                }
            }
        };

    private static string GetWindowTitle(IntPtr hwnd)
    {
        Span<char> buffer = stackalloc char[512];
        var len = GetWindowText(hwnd, buffer, buffer.Length);
        return len <= 0 ? string.Empty : new string(buffer[..len]);
    }

    private static string GetWindowClass(IntPtr hwnd)
    {
        Span<char> buffer = stackalloc char[256];
        var len = GetClassName(hwnd, buffer, buffer.Length);
        return len <= 0 ? string.Empty : new string(buffer[..len]);
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, Span<char> lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, Span<char> lpClassName, int nMaxCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
