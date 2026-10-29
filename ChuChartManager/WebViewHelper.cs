using Microsoft.Web.WebView2.Core;

namespace ChuChartManager;

public static class WebViewHelper
{
    public static bool IsRuntimeAvailable()
    {
        try
        {
            return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
        }
        catch
        {
            return false;
        }
    }

    public static void SetupCoreWebView2(CoreWebView2 coreWebView2, Uri? loopbackUrl)
    {
        coreWebView2.SetVirtualHostNameToFolderMapping(
            "ccm.invalid", StaticSettings.Wwwroot,
            CoreWebView2HostResourceAccessKind.Deny);

        if (loopbackUrl != null)
            coreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                $"globalThis.backendUrl = `{loopbackUrl.ToString().TrimEnd('/')}`");
    }

    /// <summary>
    /// 主题消息通道：前端把当前明暗配色（{ type: "setTheme", dark, bg, fg, line }）发过来，
    /// 窗口标题栏跟着一起染，避免深色界面顶着浅色标题栏。
    /// </summary>
    public static void HookWindowTheme(CoreWebView2 coreWebView2, Form form)
    {
        coreWebView2.WebMessageReceived += (_, e) => TitleBarTheme.OnWebMessage(form, e.WebMessageAsJson);
    }

    public static void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        if (e.PermissionKind is CoreWebView2PermissionKind.FileReadWrite or CoreWebView2PermissionKind.Autoplay)
        {
            e.State = CoreWebView2PermissionState.Allow;
        }
    }
}
