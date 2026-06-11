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

    public static void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        if (e.PermissionKind is CoreWebView2PermissionKind.FileReadWrite or CoreWebView2PermissionKind.Autoplay)
        {
            e.State = CoreWebView2PermissionState.Allow;
        }
    }
}
