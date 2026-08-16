namespace ChuChartManager;

/// <summary>
/// Resolves installed option packages from both supported game layouts.
/// The root-level option directory wins when the same asset directory exists
/// in both locations.
/// </summary>
public static class OptionPathResolver
{
    public static IEnumerable<string> EnumerateOptionRoots(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            yield break;

        var roots = new[]
        {
            Path.Combine(gamePath, "option"),
            Path.Combine(gamePath, "bin", "option"),
        };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            if (Directory.Exists(root) && seen.Add(Path.GetFullPath(root)))
                yield return root;
        }
    }

    public static IEnumerable<(string AssetDir, string Path)> EnumerateOptionDirectories(string gamePath)
    {
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in EnumerateOptionRoots(gamePath))
        {
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(root);
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var directory in directories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var assetDir = Path.GetFileName(directory);
                if (string.IsNullOrWhiteSpace(assetDir) || !seenNames.Add(assetDir))
                    continue;
                yield return (assetDir, directory);
            }
        }
    }

    public static string? ResolveExisting(string gamePath, string assetDir)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || string.IsNullOrWhiteSpace(assetDir))
            return null;

        foreach (var (name, path) in EnumerateOptionDirectories(gamePath))
        {
            if (string.Equals(name, assetDir, StringComparison.OrdinalIgnoreCase))
                return path;
        }

        return null;
    }

    public static string ResolveWritePath(string gamePath, string assetDir)
    {
        var existing = ResolveExisting(gamePath, assetDir);
        if (existing != null)
            return existing;

        return Path.Combine(gamePath, "bin", "option", assetDir);
    }
}
