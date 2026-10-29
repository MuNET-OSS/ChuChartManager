using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace ChuChartManager;

/// <summary>
/// 让原生窗口的标题栏 / 边框 / 窗口底色跟随前端主题，
/// 否则深色界面头顶永远是那条浅色标题栏。
/// </summary>
public static class TitleBarTheme
{
    // DWMWA_USE_IMMERSIVE_DARK_MODE：Win10 1809 (17763) 起属性号是 19，Win10 2004 (18985) 起改为 20，两个都试
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;

    // Win11 (22000+) 新增：可以自己指定标题栏配色，值为 COLORREF (0x00BBGGRR)
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    private static readonly bool IsWin11 = Environment.OSVersion.Version.Build >= 22000;

    private static readonly string SavedThemePath =
        Path.Combine(StaticSettings.AppDataDir, "window-theme.json");

    private static bool _savedLoaded;
    private static WindowThemeState? _savedTheme;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// 前端只有 localStorage 记得住主题，窗口这边靠一份 sidecar 文件，
    /// 这样下次启动先按上次的模式建窗，不会先亮再暗闪一下。
    /// </summary>
    private sealed class WindowThemeState
    {
        public bool Dark { get; set; }
        public string? Bg { get; set; }
        public string? Fg { get; set; }
        public string? Line { get; set; }
    }

    public static void Apply(Form form, bool dark, string? bgHex = null, string? fgHex = null, string? lineHex = null)
    {
        if (!form.IsHandleCreated) return;
        var hwnd = form.Handle;

        var immersive = dark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref immersive, sizeof(int)) != 0)
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref immersive, sizeof(int));

        // WebView2 是贴合填充的，底色只在启动/拉伸时露一下，跟着主题走免得闪白
        form.BackColor = ParseColor(bgHex)
                         ?? (dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(250, 250, 250));

        if (!IsWin11) return;
        if (ParseColor(bgHex) is { } caption) SetColor(hwnd, DwmwaCaptionColor, caption);
        if (ParseColor(fgHex) is { } text) SetColor(hwnd, DwmwaTextColor, text);
        if (ParseColor(lineHex) is { } border) SetColor(hwnd, DwmwaBorderColor, border);
    }

    /// <summary>启动时用上次保存的主题；没有记录就按系统明暗。</summary>
    public static void ApplySaved(Form form)
    {
        var state = LoadSaved();
        if (state == null)
        {
            Apply(form, IsSystemDark());
            return;
        }

        Apply(form, state.Dark, state.Bg, state.Fg, state.Line);
    }

    /// <summary>处理前端发来的 { type: "setTheme", dark, bg, fg, line }，其余消息忽略。</summary>
    public static void OnWebMessage(Form form, string json)
    {
        if (form.IsDisposed) return;

        WindowThemeState state;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "setTheme") return;

            state = new WindowThemeState
            {
                Dark = root.TryGetProperty("dark", out var dark) && dark.ValueKind == JsonValueKind.True,
                Bg = ReadString(root, "bg"),
                Fg = ReadString(root, "fg"),
                Line = ReadString(root, "line"),
            };
        }
        catch
        {
            return;
        }

        Apply(form, state.Dark, state.Bg, state.Fg, state.Line);
        Save(state);
    }

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void SetColor(IntPtr hwnd, int attribute, Color color)
    {
        var value = color.R | (color.G << 8) | (color.B << 16); // COLORREF 是 0x00BBGGRR
        DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
    }

    private static Color? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try
        {
            return ColorTranslator.FromHtml(hex);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String) return null;
        var text = prop.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static WindowThemeState? LoadSaved()
    {
        if (_savedLoaded) return _savedTheme;
        _savedLoaded = true;

        try
        {
            if (File.Exists(SavedThemePath))
                _savedTheme = JsonSerializer.Deserialize<WindowThemeState>(File.ReadAllText(SavedThemePath));
        }
        catch
        {
            // 文件坏了无所谓，退回系统明暗
        }

        return _savedTheme;
    }

    private static void Save(WindowThemeState state)
    {
        _savedTheme = state;
        _savedLoaded = true;

        try
        {
            Directory.CreateDirectory(StaticSettings.AppDataDir);
            File.WriteAllText(SavedThemePath, JsonSerializer.Serialize(state));
        }
        catch (Exception ex)
        {
            Log.Warn($"保存窗口主题失败: {ex.Message}");
        }
    }
}
