namespace ChuChartManager;

public static class PathGuard
{
    /// <summary>
    /// 验证 userPath 是否在 allowedBaseDir 范围内，防止路径遍历。
    /// 返回规范化后的全路径；越界时返回 null。
    /// </summary>
    public static string? EnsureWithin(string allowedBaseDir, string userPath)
    {
        var fullBase = Path.TrimEndingDirectorySeparator(Path.GetFullPath(allowedBaseDir));
        var fullTarget = Path.GetFullPath(userPath);
        if (string.Equals(fullTarget, fullBase, StringComparison.OrdinalIgnoreCase))
            return fullTarget;

        var basePrefix = fullBase + Path.DirectorySeparatorChar;
        return fullTarget.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase) ? fullTarget : null;
    }

    /// <summary>
    /// 验证 userPath 在 allowedBaseDir 范围内，是存在的文件。
    /// </summary>
    public static bool FileExistsWithin(string allowedBaseDir, string userPath, out string safePath)
    {
        safePath = "";
        var resolved = EnsureWithin(allowedBaseDir, userPath);
        if (resolved == null || !File.Exists(resolved)) return false;
        safePath = resolved;
        return true;
    }
}
