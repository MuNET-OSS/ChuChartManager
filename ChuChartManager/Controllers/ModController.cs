using System.Diagnostics;
using System.Globalization;
using ChuChartManager.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tomlyn.Model;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModController(
    AppleChuMetadataService metadataService,
    AppleChuConfigService configService,
    AppleChuDownloadService downloadService) : ControllerBase
{
    private const string AppleChuModId = "AppleChu";
    private const string GameProxyAsset = "winhttp.dll";
    private const string AmdaemonProxyAsset = "winmm.dll";

    public record ModStatus(
        bool Installed,
        string Version,
        bool AmdaemonInstalled,
        string AmdaemonVersion);

    public record ModConfigRequest(Dictionary<string, AppleChuConfigService.SectionState> Sections);
    public record VersionInfo(string Latest, string Installed, string DownloadUrl);
    public record InstallRequest(string? Channel);

    [HttpGet("status")]
    public ActionResult<ModStatus> GetStatus()
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath))
            return Ok(new ModStatus(false, "", false, ""));

        var binPath = Path.Combine(gamePath, "bin");
        var gameProxyPath = Path.Combine(binPath, GameProxyAsset);
        var amdaemonProxyPath = Path.Combine(binPath, AmdaemonProxyAsset);
        return Ok(new ModStatus(
            System.IO.File.Exists(gameProxyPath),
            ReadVersion(gameProxyPath),
            System.IO.File.Exists(amdaemonProxyPath),
            ReadFileVersion(amdaemonProxyPath)));
    }

    [HttpGet("latest-versions")]
    public async Task<ActionResult> GetLatestVersions()
    {
        var channels = await downloadService.GetChannelsAsync(HttpContext.RequestAborted);
        var binPath = string.IsNullOrEmpty(StaticSettings.GamePath)
            ? ""
            : Path.Combine(StaticSettings.GamePath, "bin");
        var latest = channels.Release?.Version ?? "";

        return Ok(new
        {
            applechu = new VersionInfo(
                latest,
                ReadVersion(Path.Combine(binPath, GameProxyAsset)),
                ""),
            amdaemon = new VersionInfo(
                latest,
                ReadFileVersion(Path.Combine(binPath, AmdaemonProxyAsset)),
                ""),
            ci = channels.Ci,
        });
    }

    [HttpPost("install-applechu")]
    public async Task<ActionResult> InstallAppleChu([FromBody] InstallRequest? request = null)
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath))
            return BadRequest("GamePath not set");

        AppleChuDownloadService.DownloadBundle bundle;
        try
        {
            bundle = await downloadService.DownloadAsync(
                request?.Channel ?? "release",
                HttpContext.RequestAborted);
        }
        catch (ArgumentException error)
        {
            return BadRequest(error.Message);
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException)
        {
            return BadRequest(error.Message);
        }
        catch (HttpRequestException error)
        {
            return StatusCode(StatusCodes.Status502BadGateway, $"下载 AppleChu 失败: {error.Message}");
        }

        AppleChuMetadataService.Metadata metadata;
        try
        {
            metadata = metadataService.Decode(bundle.GameProxy);
        }
        catch (InvalidDataException error)
        {
            return BadRequest($"winhttp.dll 未包含有效的 AppleChu 配置元数据: {error.Message}");
        }

        var binPath = Path.Combine(gamePath, "bin");
        Directory.CreateDirectory(binPath);
        await WriteAtomicallyAsync(Path.Combine(binPath, GameProxyAsset), bundle.GameProxy);
        await WriteAtomicallyAsync(Path.Combine(binPath, AmdaemonProxyAsset), bundle.AmdaemonProxy);
        configService.CreateIfMissing(gamePath, metadata);
        return Ok();
    }

    [HttpGet("manifest/{modId}")]
    public ActionResult<object> GetManifest(string modId)
    {
        if (!IsAppleChu(modId))
            return NotFound();
        if (!TryGetGamePath(out var gamePath))
            return BadRequest("GamePath not set");

        try
        {
            var metadata = metadataService.ReadInstalled(gamePath);
            return Ok(ConvertTomlValue(metadata.Manifest));
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidDataException error)
        {
            return BadRequest(error.Message);
        }
    }

    [HttpGet("config/{modId}")]
    public ActionResult<ModConfigRequest> GetConfig(string modId)
    {
        if (!IsAppleChu(modId))
            return NotFound();
        if (!TryGetGamePath(out var gamePath))
            return BadRequest("GamePath not set");

        try
        {
            var metadata = metadataService.ReadInstalled(gamePath);
            return Ok(new ModConfigRequest(configService.ReadOrCreate(gamePath, metadata)));
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidDataException error)
        {
            return BadRequest(error.Message);
        }
    }

    [HttpPut("config/{modId}")]
    public ActionResult SaveConfig(string modId, [FromBody] ModConfigRequest? request)
    {
        if (!IsAppleChu(modId))
            return NotFound();
        if (!TryGetGamePath(out var gamePath))
            return BadRequest("GamePath not set");
        if (request?.Sections == null)
            return BadRequest("配置内容不能为空");

        try
        {
            var metadata = metadataService.ReadInstalled(gamePath);
            configService.Save(gamePath, metadata, request.Sections);
            return Ok();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (Exception error) when (error is InvalidDataException or ArgumentException)
        {
            return BadRequest(error.Message);
        }
    }

    private static bool TryGetGamePath(out string gamePath)
    {
        gamePath = StaticSettings.GamePath ?? "";
        return gamePath.Length > 0;
    }

    private static bool IsAppleChu(string modId) =>
        string.Equals(modId, AppleChuModId, StringComparison.OrdinalIgnoreCase);

    private static string ReadFileVersion(string path)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return "";
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(path);
            var version = versionInfo.ProductVersion ?? versionInfo.FileVersion;
            return string.IsNullOrWhiteSpace(version) || version == "0.0.0.0" ? "" : version.Trim();
        }
        catch
        {
            return "";
        }
    }

    private string ReadVersion(string path)
    {
        var fileVersion = ReadFileVersion(path);
        if (!string.IsNullOrWhiteSpace(fileVersion))
            return fileVersion;

        if (!System.IO.File.Exists(path))
            return "";
        try
        {
            var metadata = metadataService.Read(path);
            if (metadata.Manifest.TryGetValue("mod", out var modValue)
                && modValue is TomlTable mod
                && mod.TryGetValue("version", out var versionValue)
                && versionValue is string version
                && !string.IsNullOrWhiteSpace(version))
            {
                return version.Trim();
            }
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            // The normal status endpoint reports installation separately; an invalid
            // metadata container must not turn a version lookup into a server error.
        }

        return "";
    }

    private static async Task WriteAtomicallyAsync(string path, byte[] contents)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await System.IO.File.WriteAllBytesAsync(temporaryPath, contents);
            System.IO.File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (System.IO.File.Exists(temporaryPath))
                System.IO.File.Delete(temporaryPath);
        }
    }

    private static object? ConvertTomlValue(object? value) => value switch
    {
        TomlTable table => table.ToDictionary(pair => pair.Key, pair => ConvertTomlValue(pair.Value)),
        TomlTableArray tableArray => tableArray.Select(ConvertTomlValue).ToList(),
        TomlArray array => array.Select(ConvertTomlValue).ToList(),
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        _ => value,
    };
}
