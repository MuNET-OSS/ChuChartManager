namespace ChuChartManager.Services;

public sealed class MateThumbnailService
{
    private readonly Lock convertLock = new();
    private readonly Dictionary<CacheKey, byte[]> cache = [];

    public byte[]? GetPng(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
            return null;

        var key = new CacheKey(file.FullName, file.Length, file.LastWriteTimeUtc.Ticks);
        lock (convertLock)
        {
            if (cache.TryGetValue(key, out var cached))
                return cached;

            var image = DdsHelper.ConvertDdsToPng(file.FullName);
            if (image == null)
                return null;

            foreach (var staleKey in cache.Keys.Where(item =>
                         string.Equals(item.Path, key.Path, StringComparison.OrdinalIgnoreCase)).ToList())
                cache.Remove(staleKey);
            cache[key] = image;
            return image;
        }
    }

    private readonly record struct CacheKey(string Path, long Length, long LastWriteTicks);
}
