using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace ChuChartManager.Services;

public sealed class AppleChuConfigService
{
    public sealed record SectionState(bool Enabled, Dictionary<string, object?> Entries);

    private readonly object writeLock = new();

    public Dictionary<string, SectionState> ReadOrCreate(
        string gamePath,
        AppleChuMetadataService.Metadata metadata)
    {
        var path = GetConfigPath(gamePath);
        lock (writeLock)
        {
            if (!File.Exists(path))
                WriteAtomically(path, metadata.DefaultConfigToml);
        }

        var schema = AppleChuConfigSchema.Parse(metadata);
        var document = ParseDocument(File.ReadAllText(path, Encoding.UTF8), "AppleChu.toml");
        var result = new Dictionary<string, SectionState>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in schema)
        {
            var table = GetTable(document, section.Id);
            var enabled = ReadEnabled(section, table);
            var entries = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in section.Entries)
            {
                if (AppleChuConfigSchema.IsEnableEntry(entry))
                    continue;
                if (AppleChuConfigSchema.TryGetValue(table, entry.Key, out var configured)
                    && AppleChuConfigSchema.TryNormalize(entry, configured, out var normalized))
                {
                    entries[entry.Key] = normalized;
                }
            }

            result[section.Id] = new SectionState(enabled, entries);
        }

        return result;
    }

    public void CreateIfMissing(string gamePath, AppleChuMetadataService.Metadata metadata)
    {
        var path = GetConfigPath(gamePath);
        lock (writeLock)
        {
            if (!File.Exists(path))
                WriteAtomically(path, metadata.DefaultConfigToml);
        }
    }

    public void Save(
        string gamePath,
        AppleChuMetadataService.Metadata metadata,
        IReadOnlyDictionary<string, SectionState> requestedSections)
    {
        var schema = AppleChuConfigSchema.Parse(metadata);
        var values = NormalizeRequest(schema, requestedSections);
        var output = AppleChuConfigTemplate.Apply(metadata.DefaultConfigToml, schema, values);
        lock (writeLock)
            WriteAtomically(GetConfigPath(gamePath), output);
    }

    private static IReadOnlyDictionary<string, SectionState> NormalizeRequest(
        IReadOnlyList<AppleChuConfigSectionSchema> schema,
        IReadOnlyDictionary<string, SectionState> requestedSections)
    {
        var sections = schema.ToDictionary(section => section.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var sectionId in requestedSections.Keys)
        {
            if (!sections.ContainsKey(sectionId))
                throw new ArgumentException($"未知的 AppleChu section: {sectionId}");
        }

        var result = new Dictionary<string, SectionState>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in schema)
        {
            requestedSections.TryGetValue(section.Id, out var requested);
            var enabled = section.AlwaysEnabled || (requested?.Enabled ?? section.DefaultEnabled);
            var entries = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, supplied) in requested?.Entries ?? [])
            {
                var entry = section.Entries.FirstOrDefault(item =>
                    AppleChuConfigSchema.KeysEqual(item.Key, key));
                if (entry == null)
                    throw new ArgumentException($"未知的 AppleChu 配置项: {section.Id}.{key}");
                if (AppleChuConfigSchema.IsEnableEntry(entry))
                    throw new ArgumentException($"{section.Id}.Enable 必须通过栏目开关设置");
                if (!AppleChuConfigSchema.TryNormalize(entry, supplied, out var normalized))
                    throw new ArgumentException($"{section.Id}.{entry.Key} 的值不符合 {entry.Type} 类型、范围或候选值");
                entries[entry.Key] = normalized;
            }

            result[section.Id] = new SectionState(enabled, entries);
        }

        return result;
    }

    private static TomlTable ParseDocument(string source, string name)
    {
        try
        {
            return TomlSerializer.Deserialize<TomlTable>(source)
                ?? throw new InvalidDataException($"{name} 为空");
        }
        catch (Exception error) when (error is not InvalidDataException)
        {
            throw new InvalidDataException($"{name} 不是有效的 TOML", error);
        }
    }

    private static TomlTable? GetTable(TomlTable document, string key)
    {
        return AppleChuConfigSchema.TryGetValue(document, key, out var value) ? value as TomlTable : null;
    }

    private static bool ReadEnabled(AppleChuConfigSectionSchema section, TomlTable? table)
    {
        if (section.AlwaysEnabled)
            return true;

        var enableEntry = section.Entries.FirstOrDefault(AppleChuConfigSchema.IsEnableEntry);
        if (enableEntry != null)
        {
            if (AppleChuConfigSchema.TryGetValue(table, enableEntry.Key, out var configured)
                && AppleChuConfigSchema.TryNormalize(enableEntry, configured, out var normalized)
                && normalized is bool enabled)
            {
                return enabled;
            }

            return section.DefaultEnabled;
        }

        if (AppleChuConfigSchema.TryGetValue(table, "Disabled", out var disabledValue)
            && disabledValue is bool disabled)
        {
            return !disabled;
        }

        return table != null || section.DefaultEnabled;
    }

    private static string GetConfigPath(string gamePath) => Path.Combine(gamePath, "bin", "AppleChu.toml");

    private static void WriteAtomically(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, contents, new UTF8Encoding(false));
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
