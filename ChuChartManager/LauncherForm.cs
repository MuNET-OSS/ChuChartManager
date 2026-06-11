using System.Diagnostics;

namespace ChuChartManager;

public class LauncherForm : Form
{
    private const string WebView2DownloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

    public LauncherForm()
    {
        Text = "ChuChartManager";
        Width = 520;
        Height = 280;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        if (File.Exists(iconPath))
            Icon = new Icon(iconPath);

        var message = new Label
        {
            Text = "ChuChartManager 需要 Microsoft Edge WebView2 运行时才能显示界面。\n\n"
                   + "当前系统未安装该运行时，请先安装后重新启动本程序。",
            Dock = DockStyle.Top,
            Height = 140,
            Padding = new Padding(24, 32, 24, 0),
            Font = new Font("Microsoft YaHei UI", 10),
        };

        var installButton = new Button
        {
            Text = "下载并安装 WebView2",
            Width = 200,
            Height = 40,
            Left = 24,
            Top = 170,
        };
        installButton.Click += (_, _) =>
            Process.Start(new ProcessStartInfo(WebView2DownloadUrl) { UseShellExecute = true });

        var exitButton = new Button
        {
            Text = "退出",
            Width = 120,
            Height = 40,
            Left = 360,
            Top = 170,
        };
        exitButton.Click += (_, _) => Close();

        Controls.Add(message);
        Controls.Add(installButton);
        Controls.Add(exitButton);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        Application.Exit();
    }
}
