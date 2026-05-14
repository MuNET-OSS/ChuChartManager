using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ChuChartManager;

public static partial class DwmHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWA_MICA_EFFECT = 1029;

    // Backdrop types for Windows 11 22H2+
    private const int DWMSBT_MAINWINDOW = 2;   // Mica
    private const int DWMSBT_TABBEDWINDOW = 4;  // Mica Alt
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void EnableMica(Window window, bool useDarkMode = true)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        if (useDarkMode)
        {
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }

        int backdropType = DWMSBT_MAINWINDOW;
        var result = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));

        // Windows 11 21H2 不支持 DWMWA_SYSTEMBACKDROP_TYPE，回退到旧 API
        if (result != 0)
        {
            int micaEffect = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_MICA_EFFECT, ref micaEffect, sizeof(int));
        }
    }

    public static void EnableAcrylic(Window window, bool useDarkMode = true)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        if (useDarkMode)
        {
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }

        int backdropType = DWMSBT_TRANSIENTWINDOW;
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
    }
}
