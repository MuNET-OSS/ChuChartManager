using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Tomlyn;
using Tomlyn.Model;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModController : ControllerBase
{
    public record ModInfo(string Name, string Version);
    public record ModStatus(bool LoaderInstalled, bool ProxyInstalled, List<ModInfo> Mods);
    public record ModSectionConfig(bool Enabled, Dictionary<string, object?> Entries);
    public record ModConfigRequest(Dictionary<string, ModSectionConfig> Sections);

    [HttpGet("status")]
    public ActionResult<ModStatus> GetStatus()
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath))
            return Ok(new ModStatus(false, false, []));

        var binPath = Path.Combine(gamePath, "bin");
        var loaderInstalled = System.IO.File.Exists(Path.Combine(binPath, "version.dll"));
        var proxyInstalled = System.IO.File.Exists(Path.Combine(binPath, "d3d9.dll"));
        var modsPath = Path.Combine(binPath, "mods");
        var mods = Directory.Exists(modsPath)
            ? Directory.GetFiles(modsPath, "*.dll", SearchOption.TopDirectoryOnly)
                .Select(file => new ModInfo(Path.GetFileNameWithoutExtension(file), ""))
                .OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        return Ok(new ModStatus(loaderInstalled, proxyInstalled, mods));
    }

    private const string LoaderRepo = "MuNET-OSS/ChuModLoader";
    private const string LoaderAsset = "version.dll";
    private const string ProxyAsset = "d3d9.dll";
    private const string AppleChuRepo = "MuNET-OSS/AppleChu";
    private const string AppleChuAsset = "AppleChu.dll";

    public record GitHubRelease(string Tag_name, GitHubAsset[] Assets);
    public record GitHubAsset(string Name, string Browser_download_url);
    public record VersionInfo(string Latest, string Installed, string DownloadUrl);

    private static readonly HttpClient Http = new();

    static ModController()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("ChuChartManager");
    }

    [HttpGet("latest-versions")]
    public async Task<ActionResult> GetLatestVersions()
    {
        var loader = await GetLatestRelease(LoaderRepo, LoaderAsset);
        var applechu = await GetLatestRelease(AppleChuRepo, AppleChuAsset);

        var binPath = string.IsNullOrEmpty(StaticSettings.GamePath) ? "" : Path.Combine(StaticSettings.GamePath, "bin");
        var loaderInstalled = !string.IsNullOrEmpty(binPath) && System.IO.File.Exists(Path.Combine(binPath, "version.dll"));
        var proxyInstalled = !string.IsNullOrEmpty(binPath) && System.IO.File.Exists(Path.Combine(binPath, "d3d9.dll"));
        var appleChuInstalled = !string.IsNullOrEmpty(binPath) && System.IO.File.Exists(Path.Combine(binPath, "mods", "AppleChu.dll"));

        return Ok(new
        {
            loader = new VersionInfo(loader?.Tag_name ?? "", loaderInstalled ? "installed" : "", loader?.Assets.FirstOrDefault(a => a.Name == LoaderAsset)?.Browser_download_url ?? ""),
            proxy = new VersionInfo(loader?.Tag_name ?? "", proxyInstalled ? "installed" : "", loader?.Assets.FirstOrDefault(a => a.Name == ProxyAsset)?.Browser_download_url ?? ""),
            applechu = new VersionInfo(applechu?.Tag_name ?? "", appleChuInstalled ? "installed" : "", applechu?.Assets.FirstOrDefault(a => a.Name == AppleChuAsset)?.Browser_download_url ?? ""),
        });
    }

    [HttpPost("install-loader")]
    public async Task<ActionResult> InstallLoader([FromBody] InstallRequest? request = null)
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath))
            return BadRequest("GamePath not set");

        var release = await GetLatestRelease(LoaderRepo, LoaderAsset);

        var loaderUrl = request?.Url;
        if (string.IsNullOrEmpty(loaderUrl))
            loaderUrl = release?.Assets.FirstOrDefault(a => a.Name == LoaderAsset)?.Browser_download_url;
        if (string.IsNullOrEmpty(loaderUrl))
            return NotFound("No release found");

        var binPath = Path.Combine(gamePath, "bin");
        var loaderData = await Http.GetByteArrayAsync(loaderUrl);
        await System.IO.File.WriteAllBytesAsync(Path.Combine(binPath, "version.dll"), loaderData);

        var proxyUrl = release?.Assets.FirstOrDefault(a => a.Name == ProxyAsset)?.Browser_download_url;
        if (!string.IsNullOrEmpty(proxyUrl))
        {
            var proxyData = await Http.GetByteArrayAsync(proxyUrl);
            await System.IO.File.WriteAllBytesAsync(Path.Combine(binPath, "d3d9.dll"), proxyData);
        }

        return Ok();
    }

    [HttpPost("install-applechu")]
    public async Task<ActionResult> InstallAppleChu([FromBody] InstallRequest? request = null)
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath))
            return BadRequest("GamePath not set");

        var binPath = Path.Combine(gamePath, "bin");

        var url = request?.Url;
        if (string.IsNullOrEmpty(url))
        {
            var release = await GetLatestRelease(AppleChuRepo, AppleChuAsset);
            url = release?.Assets.FirstOrDefault(a => a.Name == AppleChuAsset)?.Browser_download_url;
        }
        if (string.IsNullOrEmpty(url))
            return NotFound("No release found");

        var data = await Http.GetByteArrayAsync(url);
        var modsDir = Path.Combine(binPath, "mods");
        Directory.CreateDirectory(modsDir);
        await System.IO.File.WriteAllBytesAsync(Path.Combine(modsDir, "AppleChu.dll"), data);

        var configDest = Path.Combine(binPath, "AppleChu.toml");
        if (!System.IO.File.Exists(configDest))
        {
            var configSource = Path.Combine(StaticSettings.ExeDir, "Resources", "AppleChu", "default_config.toml");
            if (System.IO.File.Exists(configSource))
                System.IO.File.Copy(configSource, configDest);
        }

        return Ok();
    }

    public record InstallRequest(string? Url);

    private static async Task<GitHubRelease?> GetLatestRelease(string repo, string assetName)
    {
        try
        {
            var json = await Http.GetStringAsync($"https://api.github.com/repos/{repo}/releases/latest");
            return System.Text.Json.JsonSerializer.Deserialize<GitHubRelease>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    [HttpGet("manifest/{modId}")]
    public ActionResult<object> GetManifest(string modId)
    {
        var source = Path.Combine(StaticSettings.ExeDir, "Resources", modId, "manifest.toml");
        if (!System.IO.File.Exists(source))
            return NotFound();

        var model = TomlSerializer.Deserialize<TomlTable>(System.IO.File.ReadAllText(source, Encoding.UTF8));
        return Ok(ConvertTomlValue(model));
    }

    [HttpGet("config/{modId}")]
    public ActionResult<ModConfigRequest> GetConfig(string modId)
    {
        if (!TryResolveGameFile($"{modId}.toml", out var path))
            return BadRequest("GamePath not set");
        if (!System.IO.File.Exists(path))
        {
            var template = LoadDefaultConfig(modId);
            if (template == null)
                return NotFound();
            System.IO.File.WriteAllText(path, template, new UTF8Encoding(false));
        }

        var sections = ParseConfig(System.IO.File.ReadAllText(path, Encoding.UTF8));
        return Ok(new ModConfigRequest(sections));
    }

    [HttpPut("config/{modId}")]
    public ActionResult SaveConfig(string modId, [FromBody] ModConfigRequest request)
    {
        if (!TryResolveGameFile($"{modId}.toml", out var path))
            return BadRequest("GamePath not set");

        var template = LoadDefaultConfig(modId);
        var toml = template != null
            ? SerializeFromTemplate(template, request.Sections)
            : SerializeConfig(request.Sections);
        System.IO.File.WriteAllText(path, toml, new UTF8Encoding(false));
        return Ok();
    }

    private static bool TryResolveGameFile(string relativePath, out string path)
    {
        path = "";
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            return false;

        path = Path.Combine(StaticSettings.GamePath, "bin", relativePath);
        return true;
    }

    private static Dictionary<string, ModSectionConfig> ParseConfig(string toml)
    {
        var sections = new Dictionary<string, ModSectionConfig>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;

        foreach (var rawLine in toml.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0)
                continue;

            var uncommented = trimmed.StartsWith('#') ? trimmed[1..].TrimStart() : trimmed;
            if (uncommented.StartsWith('[') && uncommented.EndsWith(']'))
            {
                currentSection = uncommented[1..^1].Trim();
                if (!sections.ContainsKey(currentSection))
                    sections[currentSection] = new ModSectionConfig(!trimmed.StartsWith('#'), []);
                continue;
            }

            if (currentSection == null)
                continue;

            var isDisabledEntry = trimmed.StartsWith('#');
            var line = isDisabledEntry ? uncommented : trimmed;
            var equalIndex = line.IndexOf('=');
            if (equalIndex <= 0)
                continue;

            var key = line[..equalIndex].Trim();
            var valueText = StripInlineComment(line[(equalIndex + 1)..].Trim());
            if (string.Equals(key, "Disabled", StringComparison.OrdinalIgnoreCase)
                && ParseTomlScalar(valueText) is bool disabled)
            {
                sections[currentSection] = sections[currentSection] with { Enabled = !disabled };
                continue;
            }

            sections[currentSection].Entries[key] = ParseTomlScalar(valueText);
        }

        return sections;
    }

    private static string StripInlineComment(string value)
    {
        var inString = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '"' && (i == 0 || value[i - 1] != '\\'))
                inString = !inString;
            if (ch == '#' && !inString)
                return value[..i].TrimEnd();
        }

        return value;
    }

    private static object? ParseTomlScalar(string value)
    {
        try
        {
            var model = TomlSerializer.Deserialize<TomlTable>($"value = {value}");
            return model != null && model.TryGetValue("value", out var parsedValue)
                ? ConvertTomlValue(parsedValue)
                : value.Trim().Trim('"');
        }
        catch
        {
            return value.Trim().Trim('"');
        }
    }

    private static string? LoadDefaultConfig(string modId)
    {
        var source = Path.Combine(StaticSettings.ExeDir, "Resources", modId, "default_config.toml");
        return System.IO.File.Exists(source) ? System.IO.File.ReadAllText(source, Encoding.UTF8) : null;
    }

    private static string SerializeFromTemplate(string template, Dictionary<string, ModSectionConfig> sections)
    {
        var builder = new StringBuilder();
        string? currentSection = null;
        var usedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in template.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = rawLine.Trim();

            var uncommented = trimmed.StartsWith('#') ? trimmed[1..].TrimStart() : trimmed;
            if (uncommented.StartsWith('[') && uncommented.EndsWith(']'))
            {
                currentSection = uncommented[1..^1].Trim();
                usedEntries.Clear();
                var enabled = sections.TryGetValue(currentSection, out var s) ? s.Enabled : !trimmed.StartsWith('#');
                var prefix = enabled ? "" : "#";
                builder.Append(prefix).Append('[').Append(currentSection).AppendLine("]");
                continue;
            }

            if (currentSection != null && !trimmed.StartsWith("##") && trimmed.Length > 0)
            {
                var isCommented = trimmed.StartsWith('#');
                var line = isCommented ? uncommented : trimmed;
                var eq = line.IndexOf('=');
                if (eq > 0)
                {
                    var key = line[..eq].Trim();
                    if (sections.TryGetValue(currentSection, out var sec) && sec.Entries.TryGetValue(key, out var val))
                    {
                        var prefix = sec.Enabled ? "" : "#";
                        builder.Append(prefix).Append(key).Append(" = ").AppendLine(FormatTomlValue(val));
                        usedEntries.Add(key);
                        continue;
                    }
                }
            }

            builder.AppendLine(rawLine);
        }

        return builder.ToString();
    }

    private static string SerializeConfig(Dictionary<string, ModSectionConfig> sections)
    {
        var builder = new StringBuilder();
        foreach (var (sectionName, section) in sections)
        {
            var sectionPrefix = section.Enabled ? "" : "#";
            builder.Append(sectionPrefix).Append('[').Append(sectionName).AppendLine("]");

            foreach (var (key, value) in section.Entries)
                builder.Append(sectionPrefix).Append(key).Append(" = ").AppendLine(FormatTomlValue(value));

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string FormatTomlValue(object? value) => value switch
    {
        null => "\"\"",
        bool b => b ? "true" : "false",
        byte or sbyte or short or ushort or int or uint or long or ulong => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
        float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
        System.Text.Json.JsonElement element => FormatJsonElement(element),
        _ => $"\"{EscapeTomlString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")}\"",
    };

    private static string FormatJsonElement(System.Text.Json.JsonElement element) => element.ValueKind switch
    {
        System.Text.Json.JsonValueKind.True => "true",
        System.Text.Json.JsonValueKind.False => "false",
        System.Text.Json.JsonValueKind.Number => element.GetRawText(),
        System.Text.Json.JsonValueKind.String => $"\"{EscapeTomlString(element.GetString() ?? "")}\"",
        _ => $"\"{EscapeTomlString(element.GetRawText())}\"",
    };

    private static string EscapeTomlString(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");

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
