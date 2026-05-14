namespace ChuChartManager;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        ApplicationConfiguration.Initialize();

        Directory.CreateDirectory(StaticSettings.AppDataDir);
        StaticSettings.Config = Config.Load();

        var hasGamePath = !string.IsNullOrEmpty(StaticSettings.Config.GamePath)
                          && Directory.Exists(StaticSettings.Config.GamePath);

        if (hasGamePath)
        {
            StaticSettings.GamePath = StaticSettings.Config.GamePath;
            var scanner = new MusicScanner(StaticSettings.GamePath);
            scanner.ScanAll();
            StaticSettings.Scanner = scanner;

            AppMain.BrowserWin = new Browser();
            AppMain.BrowserWin.Show();
        }
        else
        {
            AppMain.OobeBrowserWin = new OobeBrowser();
            AppMain.OobeBrowserWin.Show();
        }

        // Form.Show() 后 SynchronizationContext 才可用
        AppMain.UiContext = SynchronizationContext.Current;

        ServerManager.StartApp(StaticSettings.Config.IsExport, url =>
        {
            if (AppMain.BrowserWin is { IsDisposed: false } browser)
                browser.Invoke(() => browser.InjectBackendUrl(url));
            if (AppMain.OobeBrowserWin is { IsDisposed: false } oobe)
                oobe.Invoke(() => oobe.InjectBackendUrl(url));
        });

        Application.Run();
    }
}
