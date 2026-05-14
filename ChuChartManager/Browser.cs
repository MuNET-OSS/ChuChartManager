using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace ChuChartManager;

public class Browser : Form
{
    private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;
    private Uri? _loopbackUrl;

    public Browser()
    {
        Text = "ChuChartManager";
        Width = 2000;
        Height = 1253;
        StartPosition = FormStartPosition.CenterScreen;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        if (File.Exists(iconPath))
            Icon = new Icon(iconPath);

        _webView = new Microsoft.Web.WebView2.WinForms.WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.Transparent
        };
        _webView.Source = new Uri("https://ccm.invalid/index.html");
        _webView.CoreWebView2InitializationCompleted += OnCoreWebView2InitializationCompleted;
        Controls.Add(_webView);
    }

    private void OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;

        WebViewHelper.SetupCoreWebView2(_webView.CoreWebView2, _loopbackUrl);
        _webView.CoreWebView2.PermissionRequested += WebViewHelper.OnPermissionRequested;
        _webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (new Uri(e.Uri).Host != "ccm.invalid")
        {
            e.Handled = true;
            Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var message = e.WebMessageAsJson;
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (root.GetProperty("type").GetString() == "setZoom")
            {
                var value = root.GetProperty("value").GetInt32();
                var dpiScale = DeviceDpi / 96.0;
                _webView.ZoomFactor = value > 0 ? value / 100.0 / dpiScale : 1.0;
            }
        }
        catch { }
    }

    public async void InjectBackendUrl(string url)
    {
        _loopbackUrl = new Uri(url);
        Text = $"ChuChartManager ({StaticSettings.GamePath})";
        await _webView.EnsureCoreWebView2Async();
        await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync($"globalThis.backendUrl = `{url}`");
        await _webView.CoreWebView2.ExecuteScriptAsync($"globalThis.backendUrl = `{url}`");
        _webView.CoreWebView2.PostWebMessageAsString(url);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _webView.Dispose();
        base.OnFormClosed(e);
        AppMain.BrowserWin = null;
        AppMain.CheckShouldExit();
    }
}
