using System.Windows;

namespace ChuChartManager;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        // var config = Config.Load();
        // if (string.IsNullOrEmpty(config.GamePath) || !Directory.Exists(config.GamePath))
        // {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var startup = new StartupWindow();
        if (startup.ShowDialog() != true)
        {
            Shutdown();
            return;
        }
        // }

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        var main = new MainWindow();
        main.Show();
    }
}
