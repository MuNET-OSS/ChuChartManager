using Microsoft.Web.WebView2.Core;

namespace ChuChartManager;

public sealed class OobeBrowser : Form
{
    private Uri? _loopbackUrl;
    private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;

    public OobeBrowser(string? loopbackUrl = null, string hash = "oobe")
    {
        if (loopbackUrl != null) _loopbackUrl = new Uri(loopbackUrl);

        _webView = new Microsoft.Web.WebView2.WinForms.WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.Transparent,
        };
        Controls.Add(_webView);

        Text = "ChuChartManager";
        ClientSize = new Size(900, 700);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        if (File.Exists(iconPath))
            Icon = new Icon(iconPath);

        _webView.Source = new Uri($"https://ccm.invalid/index.html#{hash}");
        _webView.CoreWebView2InitializationCompleted += OnCoreWebView2InitializationCompleted;

        FormClosed += (_, _) =>
        {
            _webView.Dispose();
            AppMain.OobeBrowserWin = null;
            AppMain.CheckShouldExit();
        };
    }

    private void OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;
        WebViewHelper.SetupCoreWebView2(_webView.CoreWebView2, _loopbackUrl);
        _webView.CoreWebView2.PermissionRequested += WebViewHelper.OnPermissionRequested;
    }

    public async void InjectBackendUrl(string url)
    {
        _loopbackUrl = new Uri(url);
        await _webView.EnsureCoreWebView2Async();
        await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync($"globalThis.backendUrl = `{url}`");
        await _webView.CoreWebView2.ExecuteScriptAsync($"globalThis.backendUrl = `{url}`");
        _webView.CoreWebView2.PostWebMessageAsString(url);
    }
}
