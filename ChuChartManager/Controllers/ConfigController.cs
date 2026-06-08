using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class ConfigController : ControllerBase
{
    [HttpGet]
    public ActionResult<ConfigDto> GetConfig()
    {
        return Ok(new ConfigDto
        {
            GamePath = StaticSettings.GamePath,
            HistoryPaths = StaticSettings.Config.HistoryPaths.ToList(),
            Locale = StaticSettings.Config.Locale,
            IsExport = StaticSettings.Config.IsExport,
        });
    }

    [HttpPost]
    public ActionResult SetGamePath([FromBody] string gamePath)
    {
        if (!Directory.Exists(gamePath))
            return BadRequest("路径不存在");

        if (!Directory.Exists(Path.Combine(gamePath, "data")) || !Directory.Exists(Path.Combine(gamePath, "bin")))
            return BadRequest("未找到 bin 和 data 文件夹");

        StaticSettings.Config.GamePath = gamePath;
        StaticSettings.Config.HistoryPaths.Add(gamePath);
        StaticSettings.Config.Save();
        StaticSettings.GamePath = gamePath;

        var scanner = new MusicScanner(gamePath);
        scanner.ScanAll();
        StaticSettings.Scanner = scanner;
        StaticSettings.ReadGameVersion();

        return Ok();
    }

    [HttpPost]
    public ActionResult DeleteHistoryPath([FromBody] string path)
    {
        StaticSettings.Config.HistoryPaths.Remove(path);
        StaticSettings.Config.Save();
        return Ok();
    }

    [HttpGet]
    public ActionResult<string> GetLocale()
    {
        return Ok(StaticSettings.Config.Locale);
    }

    [HttpPost]
    public ActionResult SetLocale([FromBody] string locale)
    {
        StaticSettings.Config.Locale = locale;
        StaticSettings.Config.Save();
        return Ok();
    }

    [HttpPost]
    public ActionResult OpenFolderDialog()
    {
        string? selected = null;
        var thread = new Thread(() =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择游戏根目录",
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog() == DialogResult.OK)
                selected = dialog.SelectedPath;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (selected == null)
            return Ok("");

        return Ok(selected);
    }

    [HttpPost]
    public ActionResult InitializeGameData()
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return BadRequest("GamePath not set");

        var scanner = new MusicScanner(StaticSettings.GamePath);
        scanner.ScanAll();
        StaticSettings.Scanner = scanner;
        StaticSettings.ReadGameVersion();
        return Ok();
    }

    [HttpPost]
    public ActionResult CompleteSetup([FromBody] CompleteSetupDto dto)
    {
        var exportChanged = dto.Export != StaticSettings.Config.IsExport;
        StaticSettings.Config.IsExport = dto.Export;
        StaticSettings.Config.UseAuth = dto.UseAuth;
        StaticSettings.Config.AuthUsername = dto.AuthUsername ?? "";
        StaticSettings.Config.AuthPassword = dto.AuthPassword ?? "";
        StaticSettings.Config.Save();

        if (exportChanged)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                if (ServerManager.IsRunning)
                    await ServerManager.StopAsync();

                ServerManager.StartApp(dto.Export, url =>
                {
                    if (StaticSettings.Config.IsExport)
                    {
                        AppMain.UiContext?.Post(_ =>
                            AppMain.OobeBrowserWin?.InjectBackendUrl(url), null);
                        return;
                    }

                    AppMain.ShowBrowser(url);
                    AppMain.UiContext?.Post(_ =>
                    {
                        AppMain.OobeBrowserWin?.Dispose();
                        AppMain.OobeBrowserWin = null;
                    }, null);
                });
            });
        }
        else if (!dto.Export)
        {
            var url = ServerManager.GetLoopbackUrl()
                      ?? throw new InvalidOperationException("Loopback URL is null");
            AppMain.ShowBrowser(url);
            AppMain.UiContext?.Post(_ =>
            {
                AppMain.OobeBrowserWin?.Dispose();
                AppMain.OobeBrowserWin = null;
            }, null);
        }

        return Ok();
    }

    [HttpPost]
    public ActionResult OpenMainUI()
    {
        var url = ServerManager.GetLoopbackUrl()
                  ?? throw new InvalidOperationException("Loopback URL is null");
        AppMain.ShowBrowser(url);
        AppMain.UiContext?.Post(_ =>
        {
            AppMain.OobeBrowserWin?.Dispose();
            AppMain.OobeBrowserWin = null;
        }, null);
        return Ok();
    }

    [HttpPost]
    public ActionResult SwitchToSetMode()
    {
        var url = ServerManager.GetLoopbackUrl()
                  ?? throw new InvalidOperationException("Loopback URL is null");
        AppMain.GoToModeSwitch(url);
        return Ok();
    }

    [HttpGet]
    public ActionResult<List<string>> GetLanAddresses()
    {
        var addresses = new List<string>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    addresses.Add(addr.Address.ToString());
            }
        }

        return Ok(addresses);
    }
}

public class CompleteSetupDto
{
    public bool Export { get; set; }
    public bool UseAuth { get; set; }
    public string? AuthUsername { get; set; }
    public string? AuthPassword { get; set; }
}

public class ConfigDto
{
    public string GamePath { get; set; } = "";
    public List<string> HistoryPaths { get; set; } = [];
    public string Locale { get; set; } = "";
    public bool IsExport { get; set; }
}
