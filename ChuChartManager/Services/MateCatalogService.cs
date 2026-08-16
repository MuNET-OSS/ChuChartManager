using System.Xml;
using System.Xml.Linq;
using ChuChartManager.Models;

namespace ChuChartManager.Services;

public sealed class MateCatalogService
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    public List<MateEntry> GetMates(string? source)
    {
        return EnumerateMateAssets(source)
            .Select(asset => asset.Entry)
            .OrderBy(entry => entry.AssetDir, PathComparer)
            .ThenBy(entry => entry.NumericId)
            .ThenBy(entry => entry.Id, PathComparer)
            .ToList();
    }

    public MateAsset? FindMate(string assetDir, string mateId)
    {
        if (string.IsNullOrWhiteSpace(assetDir) || string.IsNullOrWhiteSpace(mateId))
            return null;

        foreach (var (mateRoot, source) in EnumerateMateRoots(assetDir))
        {
            var directory = Directory.EnumerateDirectories(mateRoot)
                .FirstOrDefault(path => string.Equals(Path.GetFileName(path), mateId, StringComparison.OrdinalIgnoreCase));
            if (directory != null)
                return ParseMateDirectory(directory, source);
        }

        return null;
    }

    private static IEnumerable<MateAsset> EnumerateMateAssets(string? source)
    {
        foreach (var (mateRoot, assetDir) in EnumerateMateRoots(source))
        {
            foreach (var directory in Directory.EnumerateDirectories(mateRoot).OrderBy(path => path, PathComparer))
            {
                var asset = ParseMateDirectory(directory, assetDir);
                if (asset != null)
                    yield return asset;
            }
        }
    }

    private static IEnumerable<(string Path, string AssetDir)> EnumerateMateRoots(string? source)
    {
        if (string.IsNullOrEmpty(StaticSettings.GamePath))
            yield break;

        if (source == null || string.Equals(source, "A000", StringComparison.OrdinalIgnoreCase))
        {
            var baseRoot = Path.Combine(StaticSettings.GamePath, "data", "A000", "mate");
            if (Directory.Exists(baseRoot))
                yield return (baseRoot, "A000");
        }

        foreach (var (assetDir, optionDirectory) in OptionPathResolver.EnumerateOptionDirectories(StaticSettings.GamePath))
        {
            if (source != null && !string.Equals(source, assetDir, StringComparison.OrdinalIgnoreCase))
                continue;

            var mateRoot = Path.Combine(optionDirectory, "mate");
            if (Directory.Exists(mateRoot))
                yield return (mateRoot, assetDir);
        }
    }

    private static MateAsset? ParseMateDirectory(string directory, string assetDir)
    {
        var mateId = Path.GetFileName(directory);
        var xmlPath = Path.Combine(directory, "Mate.xml");
        if (!File.Exists(xmlPath))
            return null;

        try
        {
            using var reader = XmlReader.Create(xmlPath, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            });
            var document = XDocument.Load(reader);
            var root = document.Root;
            if (root == null)
                return null;

            var emotePath = ResolveFile(directory, root.Element("emoteFile")?.Element("path")?.Value);
            if (emotePath == null)
                return null;

            var name = root.Element("name")?.Element("str")?.Value?.Trim();
            if (string.IsNullOrEmpty(name))
                name = mateId;

            var numericId = int.TryParse(root.Element("name")?.Element("id")?.Value, out var parsedId)
                ? parsedId
                : ParseNumericId(mateId);
            var thumbnailPath = ResolveFile(directory, root.Element("image")?.Element("path")?.Value);
            var actions = root.Element("actions")?.Elements("MateActionData")
                .Select(action => ParseAction(action, directory))
                .Where(action => action != null)
                .Cast<MateAction>()
                .ToList() ?? [];

            var entry = new MateEntry(
                mateId,
                numericId,
                name,
                assetDir,
                thumbnailPath != null,
                new FileInfo(emotePath).Length,
                actions);
            return new MateAsset(entry, emotePath, thumbnailPath);
        }
        catch (XmlException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static MateAction? ParseAction(XElement element, string directory)
    {
        if (!int.TryParse(element.Element("mateActionId")?.Value, out var id))
            return null;

        var lipSyncPath = ResolveFile(directory, element.Element("lipSync")?.Element("path")?.Value);
        return new MateAction(
            id,
            ParseInt(element.Element("actionType")?.Value),
            element.Element("emote")?.Value?.Trim() ?? "",
            ParseBool(element.Element("isVoice")?.Value),
            ParseBool(element.Element("isLipSync")?.Value) && lipSyncPath != null,
            ParseBool(element.Element("isSpecialMotion")?.Value),
            ParseInt(element.Element("msecEmoteEnd")?.Value));
    }

    private static string? ResolveFile(string directory, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
            return null;

        var candidate = Path.Combine(directory, fileName);
        return PathGuard.FileExistsWithin(directory, candidate, out var safePath) ? safePath : null;
    }

    private static int ParseNumericId(string mateId)
    {
        var digits = new string(mateId.Where(char.IsAsciiDigit).ToArray());
        return int.TryParse(digits, out var id) ? id : 0;
    }

    private static int ParseInt(string? value) => int.TryParse(value, out var result) ? result : 0;
    private static bool ParseBool(string? value) => bool.TryParse(value, out var result) && result;
}
