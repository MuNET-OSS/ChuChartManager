using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace ChuChartManager;

public partial class WebPreviewWindow : Window
{
    private readonly string _electronPreviewDir;
    private readonly string? _chartPath;
    private readonly string? _bgmPath;

    public WebPreviewWindow(string? chartPath = null, string? bgmPath = null)
    {
        InitializeComponent();

        _chartPath = chartPath;
        _bgmPath = bgmPath;

        _electronPreviewDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElectronPreview"));

        if (!Directory.Exists(_electronPreviewDir))
        {
            _electronPreviewDir = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "ElectronPreview"));
        }

        Loaded += async (_, _) => await InitWebView();
    }

    private async Task InitWebView()
    {
        var env = await CoreWebView2Environment.CreateAsync();
        await WebView.EnsureCoreWebView2Async(env);

        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "preview.local", _electronPreviewDir,
            CoreWebView2HostResourceAccessKind.Allow);

        await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildBridgeScript());

        WebView.CoreWebView2.WebMessageReceived += OnWebMessage;
        WebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        WebView.CoreWebView2.Navigate("https://preview.local/index.html");
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;

        if (_chartPath != null && File.Exists(_chartPath))
        {
            await Task.Delay(300);
            await LoadChart(_chartPath, _bgmPath);
        }
    }

    private string BuildBridgeScript()
    {
        return """
        (function() {
            const searchDirs = ['textures/', 'textures/arcade/', 'textures_original/'];

            window.electronAPI = {
                readTexture: async function(name) {
                    for (const dir of searchDirs) {
                        try {
                            const r = await fetch('https://preview.local/' + dir + name);
                            if (r.ok) return await r.arrayBuffer();
                        } catch {}
                    }
                    return null;
                },

                readFile: function(path) {
                    return new Promise(function(resolve) {
                        const id = '_rf_' + Date.now() + '_' + Math.random();
                        window[id] = resolve;
                        window.chrome.webview.postMessage(JSON.stringify({
                            type: 'readFile', path: path, callbackId: id
                        }));
                    });
                },

                openUgc: function() {
                    return new Promise(function(resolve) {
                        const id = '_ou_' + Date.now();
                        window[id] = resolve;
                        window.chrome.webview.postMessage(JSON.stringify({
                            type: 'openUgc', callbackId: id
                        }));
                    });
                },

                openBgm: function() {
                    return new Promise(function(resolve) {
                        const id = '_ob_' + Date.now();
                        window[id] = resolve;
                        window.chrome.webview.postMessage(JSON.stringify({
                            type: 'openBgm', callbackId: id
                        }));
                    });
                },

                listTextures: async function() {
                    return [];
                },

                onLoadChart: function(callback) {
                    window._onLoadChartCb = callback;
                }
            };

            window.chrome.webview.addEventListener('message', function(e) {
                var msg = (typeof e.data === 'string') ? JSON.parse(e.data) : e.data;
                if (msg.callbackId && window[msg.callbackId]) {
                    window[msg.callbackId](msg.result);
                    delete window[msg.callbackId];
                }
            });
        })();
        """;
    }

    private async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = JsonDocument.Parse(e.WebMessageAsJson);
            var root = json.RootElement;
            string type = root.GetProperty("type").GetString() ?? "";
            string callbackId = root.GetProperty("callbackId").GetString() ?? "";

            switch (type)
            {
                case "readFile":
                {
                    string path = root.GetProperty("path").GetString() ?? "";
                    if (File.Exists(path))
                    {
                        byte[] data = await File.ReadAllBytesAsync(path);
                        string b64 = Convert.ToBase64String(data);
                        await WebView.CoreWebView2.ExecuteScriptAsync(
                            $$"""
                            (function() {
                                var b64 = '{{b64}}';
                                var bin = atob(b64);
                                var buf = new ArrayBuffer(bin.length);
                                var view = new Uint8Array(buf);
                                for (var i = 0; i < bin.length; i++) view[i] = bin.charCodeAt(i);
                                if (window['{{callbackId}}']) {
                                    window['{{callbackId}}'](buf);
                                    delete window['{{callbackId}}'];
                                }
                            })();
                            """);
                    }
                    else
                    {
                        await ResolveCallback(callbackId, "null");
                    }
                    break;
                }
                case "openUgc":
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "选择 UGC 谱面文件",
                        Filter = "UGC 谱面|*.ugc|所有文件|*.*",
                    };
                    string? result = dlg.ShowDialog(this) == true ? dlg.FileName : null;
                    await ResolveCallback(callbackId, result == null ? "null" : $"'{EscapeJs(result)}'");
                    break;
                }
                case "openBgm":
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "选择 BGM 音频文件",
                        Filter = "音频文件|*.ogg;*.wav;*.mp3;*.flac|所有文件|*.*",
                    };
                    string? result = dlg.ShowDialog(this) == true ? dlg.FileName : null;
                    await ResolveCallback(callbackId, result == null ? "null" : $"'{EscapeJs(result)}'");
                    break;
                }
            }
        }
        catch
        {
            // 忽略解析错误
        }
    }

    private async Task ResolveCallback(string callbackId, string jsValue)
    {
        await WebView.CoreWebView2.ExecuteScriptAsync(
            $"if(window['{callbackId}']){{window['{callbackId}']({jsValue});delete window['{callbackId}'];}}");
    }

    private static string EscapeJs(string s) =>
        s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");

    internal async Task LoadChart(string chartPath, string? bgmPath = null)
    {
        await WebView.CoreWebView2.ExecuteScriptAsync(
            $"if(window._onLoadChartCb) window._onLoadChartCb({{ugcPath:'{EscapeJs(chartPath)}',bgmPath:{(bgmPath != null ? $"'{EscapeJs(bgmPath)}'" : "null")}}});");
    }
}
