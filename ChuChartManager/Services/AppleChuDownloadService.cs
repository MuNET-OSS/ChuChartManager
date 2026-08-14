using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChuChartManager.Services;

public sealed class AppleChuDownloadService
{
    private const string Repository = "MuNET-OSS/AppleChu";
    private const string Workflow = "build.yml";
    private const string PackageAsset = "AppleChu.zip";
    private const string CiArtifactName = "AppleChu";
    private const string GameProxyAsset = "winhttp.dll";
    private const string AmdaemonProxyAsset = "winmm.dll";
    private const string ExampleConfigAsset = "AppleChu.example.toml";
    private const string FullConfigAsset = "AppleChu.full.toml";
    private const int MaximumDownloadLength = 64 * 1024 * 1024;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(2);
    private static readonly HttpClient Http = CreateHttpClient();

    public sealed record ReleaseChannel(string Version);
    public sealed record CiChannel(string Version, string Commit, DateTimeOffset CreatedAt);
    public sealed record ChannelInfo(ReleaseChannel? Release, CiChannel? Ci);
    public sealed record DownloadBundle(byte[] GameProxy, byte[] AmdaemonProxy);

    private sealed record ReleaseDescriptor(string Version, AssetDescriptor Package);
    private sealed record CiDescriptor(
        long RunId,
        int RunNumber,
        string Commit,
        DateTimeOffset CreatedAt,
        AssetDescriptor Package);
    private sealed record AssetDescriptor(string Name, string Url, string? Digest, AssetSource Source);
    private sealed record Snapshot(ReleaseDescriptor? Release, CiDescriptor? Ci);

    private enum AssetSource
    {
        Release,
        Ci,
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("assets")] GitHubReleaseAsset[]? Assets);

    private sealed record GitHubReleaseAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string DownloadUrl,
        [property: JsonPropertyName("digest")] string? Digest);

    private sealed record WorkflowRunsResponse(
        [property: JsonPropertyName("workflow_runs")] GitHubWorkflowRun[]? WorkflowRuns);

    private sealed record GitHubWorkflowRun(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("run_number")] int RunNumber,
        [property: JsonPropertyName("head_branch")] string HeadBranch,
        [property: JsonPropertyName("head_sha")] string HeadSha,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("conclusion")] string? Conclusion,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

    private sealed record ArtifactsResponse(
        [property: JsonPropertyName("artifacts")] GitHubArtifact[]? Artifacts);

    private sealed record GitHubArtifact(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("expired")] bool Expired,
        [property: JsonPropertyName("digest")] string? Digest,
        [property: JsonPropertyName("workflow_run")] ArtifactWorkflowRun? WorkflowRun);

    private sealed record ArtifactWorkflowRun([property: JsonPropertyName("id")] long Id);

    private readonly SemaphoreSlim cacheLock = new(1, 1);
    private Snapshot? cachedSnapshot;
    private DateTimeOffset cacheExpiresAt;

    public async Task<ChannelInfo> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return new ChannelInfo(
            snapshot.Release == null ? null : new ReleaseChannel(snapshot.Release.Version),
            snapshot.Ci == null
                ? null
                : new CiChannel(
                    $"CI #{snapshot.Ci.RunNumber}",
                    snapshot.Ci.Commit,
                    snapshot.Ci.CreatedAt));
    }

    public async Task<DownloadBundle> DownloadAsync(
        string channel,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return channel.ToLowerInvariant() switch
        {
            "release" when snapshot.Release != null => await DownloadReleaseAsync(snapshot.Release, cancellationToken),
            "ci" when snapshot.Ci != null => await DownloadCiAsync(snapshot.Ci, cancellationToken),
            "release" => throw new InvalidOperationException("没有可用的 AppleChu Release 构建"),
            "ci" => throw new InvalidOperationException("没有可用的 AppleChu CI 构建"),
            _ => throw new ArgumentException("无效的 AppleChu 下载渠道", nameof(channel)),
        };
    }

    private async Task<Snapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        if (cachedSnapshot != null && DateTimeOffset.UtcNow < cacheExpiresAt)
            return cachedSnapshot;

        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (cachedSnapshot != null && DateTimeOffset.UtcNow < cacheExpiresAt)
                return cachedSnapshot;

            var releaseTask = GetLatestReleaseAsync(cancellationToken);
            var ciTask = GetLatestCiAsync(cancellationToken);
            await Task.WhenAll(releaseTask, ciTask);
            cachedSnapshot = new Snapshot(await releaseTask, await ciTask);
            cacheExpiresAt = DateTimeOffset.UtcNow.Add(CacheLifetime);
            return cachedSnapshot;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    private static async Task<ReleaseDescriptor?> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var release = await GetJsonAsync<GitHubRelease>(
                $"https://api.github.com/repos/{Repository}/releases/latest",
                cancellationToken);
            var package = FindReleaseAsset(release, PackageAsset);
            return release == null || package == null
                ? null
                : new ReleaseDescriptor(release.TagName, package);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<CiDescriptor?> GetLatestCiAsync(CancellationToken cancellationToken)
    {
        try
        {
            var runs = await GetJsonAsync<WorkflowRunsResponse>(
                $"https://api.github.com/repos/{Repository}/actions/workflows/{Workflow}/runs" +
                "?branch=main&status=success&event=push&per_page=1",
                cancellationToken);
            var run = runs?.WorkflowRuns?.FirstOrDefault(item =>
                item.HeadBranch == "main"
                && item.Event == "push"
                && item.Conclusion == "success");
            if (run == null || run.HeadSha.Length < 7)
                return null;

            var artifactResponse = await GetJsonAsync<ArtifactsResponse>(
                $"https://api.github.com/repos/{Repository}/actions/runs/{run.Id}/artifacts?per_page=100",
                cancellationToken);
            var package = FindCiArtifact(artifactResponse, run.Id);
            if (package == null)
                return null;

            return new CiDescriptor(
                run.Id,
                run.RunNumber,
                run.HeadSha,
                run.CreatedAt,
                package);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AssetDescriptor? FindReleaseAsset(GitHubRelease? release, string name)
    {
        var asset = release?.Assets?.FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (asset == null || !IsAllowedReleaseUrl(asset.DownloadUrl))
            return null;
        return new AssetDescriptor(name, asset.DownloadUrl, asset.Digest, AssetSource.Release);
    }

    private static AssetDescriptor? FindCiArtifact(ArtifactsResponse? response, long runId)
    {
        var artifact = response?.Artifacts?.FirstOrDefault(item =>
            !item.Expired
            && item.WorkflowRun?.Id == runId
            && string.Equals(item.Name, CiArtifactName, StringComparison.OrdinalIgnoreCase));
        if (artifact?.Digest == null)
            return null;

        var encodedName = Uri.EscapeDataString(CiArtifactName);
        var url = $"https://nightly.link/{Repository}/actions/runs/{runId}/{encodedName}.zip";
        return new AssetDescriptor(PackageAsset, url, artifact.Digest, AssetSource.Ci);
    }

    private static async Task<DownloadBundle> DownloadReleaseAsync(
        ReleaseDescriptor release,
        CancellationToken cancellationToken)
    {
        var package = await DownloadAssetAsync(release.Package, cancellationToken);
        return ExtractPackage(package);
    }

    private static async Task<DownloadBundle> DownloadCiAsync(
        CiDescriptor ci,
        CancellationToken cancellationToken)
    {
        var package = await DownloadAssetAsync(ci.Package, cancellationToken);
        return ExtractPackage(package);
    }

    private static async Task<byte[]> DownloadAssetAsync(
        AssetDescriptor asset,
        CancellationToken cancellationToken)
    {
        if (asset.Source == AssetSource.Ci && !IsAllowedCiUrl(asset.Url))
            throw new InvalidDataException("AppleChu CI 下载地址无效");
        if (asset.Source == AssetSource.Release && !IsAllowedReleaseUrl(asset.Url))
            throw new InvalidDataException("AppleChu Release 下载地址无效");

        var bytes = await Http.GetByteArrayAsync(asset.Url, cancellationToken);
        if (bytes.Length == 0 || bytes.Length > MaximumDownloadLength)
            throw new InvalidDataException($"{asset.Name} 下载文件大小无效");
        if (!VerifyDigest(bytes, asset.Digest, asset.Source == AssetSource.Ci))
            throw new InvalidDataException($"{asset.Name} 校验失败，文件可能已损坏或被篡改");
        return bytes;
    }

    private static DownloadBundle ExtractPackage(byte[] archiveBytes)
    {
        using var stream = new MemoryStream(archiveBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var expectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            GameProxyAsset,
            AmdaemonProxyAsset,
            ExampleConfigAsset,
            FullConfigAsset,
        };
        var entries = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name))
            .ToArray();
        if (entries.Length != expectedNames.Count
            || entries.Any(entry => !expectedNames.Contains(entry.FullName))
            || expectedNames.Any(name => entries.Count(entry =>
                string.Equals(entry.FullName, name, StringComparison.OrdinalIgnoreCase)) != 1))
        {
            throw new InvalidDataException(
                $"{PackageAsset} 必须只包含 {string.Join("、", expectedNames)}");
        }

        foreach (var entry in entries)
        {
            if (entry.Length <= 0 || entry.Length > MaximumDownloadLength)
                throw new InvalidDataException($"{PackageAsset} 中的 {entry.FullName} 大小无效");
        }

        return new DownloadBundle(
            ReadEntry(entries, GameProxyAsset),
            ReadEntry(entries, AmdaemonProxyAsset));
    }

    private static byte[] ReadEntry(IReadOnlyList<ZipArchiveEntry> entries, string name)
    {
        var entry = entries.Single(item =>
            string.Equals(item.FullName, name, StringComparison.OrdinalIgnoreCase));
        using var input = entry.Open();
        using var output = new MemoryStream(checked((int)entry.Length));
        input.CopyTo(output);
        return output.ToArray();
    }

    private static bool VerifyDigest(byte[] data, string? digest, bool required)
    {
        if (string.IsNullOrWhiteSpace(digest))
            return !required;
        if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            return false;

        var expected = digest["sha256:".Length..];
        var actual = Convert.ToHexString(SHA256.HashData(data));
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedReleaseUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.StartsWith(
            $"/{Repository}/releases/download/",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedCiUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host.Equals("nightly.link", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.StartsWith(
            $"/{Repository}/actions/runs/",
            StringComparison.OrdinalIgnoreCase);

    private static async Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        var json = await Http.GetStringAsync(url, cancellationToken);
        return JsonSerializer.Deserialize<T>(json);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ChuChartManager");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }
}
