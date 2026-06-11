using SingleInstanceCore;

namespace ChuChartManager;

public partial class AppMain : ISingleInstance
{
    public static Browser? BrowserWin { get; set; }
    public static OobeBrowser? OobeBrowserWin { get; set; }
    public static SynchronizationContext? UiContext { get; set; }

    public static void ShowBrowser(string loopbackUrl)
    {
        UiContext?.Post(_ =>
        {
            if (BrowserWin is { IsDisposed: false })
            {
                BrowserWin.InjectBackendUrl(loopbackUrl);
                BrowserWin.Activate();
            }
            else
            {
                BrowserWin = new Browser();
                BrowserWin.Show();
                BrowserWin.InjectBackendUrl(loopbackUrl);
            }
        }, null);
    }

    public static void GoToModeSwitch(string loopbackUrl)
    {
        UiContext?.Post(_ =>
        {
            if (BrowserWin is { IsDisposed: false })
            {
                BrowserWin.Dispose();
                BrowserWin = null;
            }

            if (OobeBrowserWin is { IsDisposed: false })
            {
                OobeBrowserWin.Dispose();
                OobeBrowserWin = null;
            }

            OobeBrowserWin = new OobeBrowser(loopbackUrl, "mode-select");
            OobeBrowserWin.Show();
            OobeBrowserWin.Activate();
        }, null);
    }

    public static void CheckShouldExit()
    {
        var browserAlive = BrowserWin is { IsDisposed: false };
        var oobeAlive = OobeBrowserWin is { IsDisposed: false };
        if (!browserAlive && !oobeAlive)
        {
            Application.Exit();
        }
    }

    /// <summary>第二个实例启动时，把已有窗口带到前台</summary>
    public void OnInstanceInvoked(string[] args)
    {
        UiContext?.Post(_ =>
        {
            var win = (Form?)(BrowserWin is { IsDisposed: false } ? BrowserWin : null)
                      ?? (OobeBrowserWin is { IsDisposed: false } ? OobeBrowserWin : null);
            if (win == null) return;

            if (win.WindowState == FormWindowState.Minimized)
                win.WindowState = FormWindowState.Normal;
            win.Activate();
        }, null);
    }
}
