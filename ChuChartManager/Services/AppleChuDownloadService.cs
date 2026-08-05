using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChuChartManager.Services;

public sealed class AppleChuDownloadService
{
    private const string Repository = "MuNET-OSS/AppleChu";
    private const string Workflow = "build.yml";
    private const string GameProxyAsset = "winhttp.dll";
    private const string AmdaemonProxyAsset = "winmm.dll";
    private const int MaximumDownloadLength = 64 * 1024 * 1024;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(2);
    private static readonly HttpClient Http = CreateHttpClient();

    public sealed record ReleaseChannel(string Version);
    public sealed record CiChannel(string Version, string Commit, DateTimeOffset CreatedAt);
    public sealed record ChannelInfo(ReleaseChannel? Release, CiChannel? Ci);
    public sealed record DownloadBundle(byte[] GameProxy, byte[] AmdaemonProxy);

    private sealed record ReleaseDescriptor(string Version, AssetDescriptor GameProxy, AssetDescriptor AmdaemonProxy);
    private sealed record CiDescriptor(long RunId, int RunNumber, string Commit, DateTimeOffset CreatedAt,
        AssetDescriptor GameProxy, AssetDescriptor AmdaemonProxy);
    private sealed record AssetDescriptor(string Name, string Url, string? Digest, bool IsArchive);
    private sealed record Snapshot(ReleaseDescriptor? Release, CiDescriptor? Ci);

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
            var gameProxy = FindReleaseAsset(release, GameProxyAsset);
            var amdaemonProxy = FindReleaseAsset(release, AmdaemonProxyAsset);
            return release == null || gameProxy == null || amdaemonProxy == null
                ? null
                : new ReleaseDescriptor(release.TagName, gameProxy, amdaemonProxy);
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
            var gameProxy = FindCiArtifact(artifactResponse, run.Id, GameProxyAsset);
            var amdaemonProxy = FindCiArtifact(artifactResponse, run.Id, AmdaemonProxyAsset);
            if (gameProxy == null || amdaemonProxy == null)
                return null;

            return new CiDescriptor(
                run.Id,
                run.RunNumber,
                run.HeadSha,
                run.CreatedAt,
                gameProxy,
                amdaemonProxy);
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
        return new AssetDescriptor(name, asset.DownloadUrl, asset.Digest, false);
    }

    private static AssetDescriptor? FindCiArtifact(ArtifactsResponse? response, long runId, string name)
    {
        var artifact = response?.Artifacts?.FirstOrDefault(item =>
            !item.Expired
            && item.WorkflowRun?.Id == runId
            && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (artifact?.Digest == null)
            return null;

        var encodedName = Uri.EscapeDataString(name);
        var url = $"https://nightly.link/{Repository}/actions/runs/{runId}/{encodedName}.zip";
        return new AssetDescriptor(name, url, artifact.Digest, true);
    }

    private static async Task<DownloadBundle> DownloadReleaseAsync(
        ReleaseDescriptor release,
        CancellationToken cancellationToken)
    {
        var gameProxyTask = DownloadAssetAsync(release.GameProxy, cancellationToken);
        var amdaemonProxyTask = DownloadAssetAsync(release.AmdaemonProxy, cancellationToken);
        await Task.WhenAll(gameProxyTask, amdaemonProxyTask);
        return new DownloadBundle(await gameProxyTask, await amdaemonProxyTask);
    }

    private static async Task<DownloadBundle> DownloadCiAsync(
        CiDescriptor ci,
        CancellationToken cancellationToken)
    {
        var gameArchiveTask = DownloadAssetAsync(ci.GameProxy, cancellationToken);
        var amdaemonArchiveTask = DownloadAssetAsync(ci.AmdaemonProxy, cancellationToken);
        await Task.WhenAll(gameArchiveTask, amdaemonArchiveTask);
        return new DownloadBundle(
            ExtractArtifact(await gameArchiveTask, GameProxyAsset),
            ExtractArtifact(await amdaemonArchiveTask, AmdaemonProxyAsset));
    }

    private static async Task<byte[]> DownloadAssetAsync(
        AssetDescriptor asset,
        CancellationToken cancellationToken)
    {
        if (asset.IsArchive && !IsAllowedCiUrl(asset.Url))
            throw new InvalidDataException("AppleChu CI 下载地址无效");
        if (!asset.IsArchive && !IsAllowedReleaseUrl(asset.Url))
            throw new InvalidDataException("AppleChu Release 下载地址无效");

        var bytes = await Http.GetByteArrayAsync(asset.Url, cancellationToken);
        if (bytes.Length == 0 || bytes.Length > MaximumDownloadLength)
            throw new InvalidDataException($"{asset.Name} 下载文件大小无效");
        if (!VerifyDigest(bytes, asset.Digest, asset.IsArchive))
            throw new InvalidDataException($"{asset.Name} 校验失败，文件可能已损坏或被篡改");
        return bytes;
    }

    private static byte[] ExtractArtifact(byte[] archiveBytes, string expectedName)
    {
        using var stream = new MemoryStream(archiveBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entries = archive.Entries.Where(entry =>
            string.Equals(entry.FullName, expectedName, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (entries.Length != 1 || archive.Entries.Count != 1)
            throw new InvalidDataException($"CI artifact 必须只包含 {expectedName}");

        var entry = entries[0];
        if (entry.Length <= 0 || entry.Length > MaximumDownloadLength)
            throw new InvalidDataException($"CI artifact 中的 {expectedName} 大小无效");
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
