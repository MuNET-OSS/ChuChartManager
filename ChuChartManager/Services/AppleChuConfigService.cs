using System.Globalization;
using System.Text;
using System.Text.Json;
using Tomlyn;
using Tomlyn.Model;

namespace ChuChartManager.Services;

public sealed class AppleChuConfigService
{
    public sealed record SectionState(bool Enabled, Dictionary<string, object?> Entries);

    private sealed record EntrySchema(
        string Key,
        string Type,
        object? Default,
        long? Min,
        long? Max,
        IReadOnlyList<object?>? Options);

    private sealed record SectionSchema(
        string Id,
        bool DefaultEnabled,
        IReadOnlyList<EntrySchema> Entries);

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

        var schema = ParseSchema(metadata);
        var document = ParseDocument(File.ReadAllText(path, Encoding.UTF8), "AppleChu.toml");
        var result = new Dictionary<string, SectionState>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in schema)
        {
            var table = GetTable(document, section.Id);
            var enabled = table?.TryGetValue("enable", out var enabledValue) == true
                          && enabledValue is bool configured
                ? configured
                : section.DefaultEnabled;
            var entries = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in section.Entries)
            {
                var value = table?.TryGetValue(entry.Key, out var configuredValue) == true
                            && TryNormalizeValue(entry, configuredValue, out var normalized)
                    ? normalized
                    : CloneValue(entry.Default);
                entries[entry.Key] = value;
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
        var schema = ParseSchema(metadata);
        ValidateRequestKeys(schema, requestedSections);

        var values = new Dictionary<string, SectionState>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in schema)
        {
            requestedSections.TryGetValue(section.Id, out var requested);
            var entries = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in section.Entries)
            {
                var candidate = requested?.Entries.TryGetValue(entry.Key, out var supplied) == true
                    ? supplied
                    : entry.Default;
                if (!TryNormalizeValue(entry, candidate, out var normalized))
                    throw new ArgumentException($"{section.Id}.{entry.Key} 的值不符合 {entry.Type} 类型、范围或候选值");
                entries[entry.Key] = normalized;
            }

            values[section.Id] = new SectionState(
                requested?.Enabled ?? section.DefaultEnabled,
                entries);
        }

        var output = ApplyToTemplate(metadata.DefaultConfigToml, schema, values);
        lock (writeLock)
            WriteAtomically(GetConfigPath(gamePath), output);
    }

    private static IReadOnlyList<SectionSchema> ParseSchema(AppleChuMetadataService.Metadata metadata)
    {
        var config = (TomlTable)metadata.Manifest["config"];
        var rawSections = (TomlTableArray)config["sections"];
        var result = new List<SectionSchema>(rawSections.Count);

        foreach (var rawSection in rawSections)
        {
            var id = (string)rawSection["id"];
            var defaultEnabled = rawSection.TryGetValue("default_enabled", out var enabledValue)
                                 && enabledValue is bool enabled
                ? enabled
                : false;
            // 旧版模板可能把默认关闭的 section 和字段整体注释掉；manifest 是权威默认值。
            // 旧版 AppleChu 默认模板没有显式 enable；保存时会迁移为新格式。

            var entries = new List<EntrySchema>();
            if (rawSection.TryGetValue("entries", out var entriesValue)
                && entriesValue is TomlTableArray rawEntries)
            {
                foreach (var rawEntry in rawEntries)
                {
                    var key = rawEntry.TryGetValue("key", out var keyValue) ? keyValue as string : null;
                    var type = rawEntry.TryGetValue("type", out var typeValue) ? typeValue as string : null;
                    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(type))
                        throw new InvalidDataException($"{id} 包含无效的配置项元数据");
                    if (!rawEntry.TryGetValue("default", out var defaultValue))
                        throw new InvalidDataException($"manifest 缺少 {id}.{key} 默认值");
                    var entry = new EntrySchema(
                        key,
                        type,
                        NormalizeTomlValue(defaultValue),
                        ReadInteger(rawEntry, "min"),
                        ReadInteger(rawEntry, "max"),
                        null);
                    if (!TryNormalizeValue(entry, entry.Default, out _))
                        throw new InvalidDataException($"manifest 中 {id}.{key} 的默认值无效");
                    entry = entry with { Options = ReadOptions(rawEntry, id, entry) };
                    if (!TryNormalizeValue(entry, entry.Default, out _))
                        throw new InvalidDataException($"manifest 中 {id}.{key} 的默认值不在 options 内");
                    entries.Add(entry);
                }
            }

            result.Add(new SectionSchema(id, defaultEnabled, entries));
        }

        return result;
    }

    private static void ValidateRequestKeys(
        IReadOnlyList<SectionSchema> schema,
        IReadOnlyDictionary<string, SectionState> requestedSections)
    {
        var sections = schema.ToDictionary(section => section.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var (sectionId, state) in requestedSections)
        {
            if (!sections.TryGetValue(sectionId, out var section))
                throw new ArgumentException($"未知的 AppleChu section: {sectionId}");
            var entries = section.Entries.Select(entry => entry.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var key in state.Entries.Keys)
            {
                if (!entries.Contains(key))
                    throw new ArgumentException($"未知的 AppleChu 配置项: {sectionId}.{key}");
            }
        }
    }

    private static string ApplyToTemplate(
        string template,
        IReadOnlyList<SectionSchema> schema,
        IReadOnlyDictionary<string, SectionState> values)
    {
        var sections = schema.ToDictionary(section => section.Id, StringComparer.OrdinalIgnoreCase);
        var seenSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEntries = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var templateEnableSections = FindTemplateEnableSections(template, sections);
        var output = new StringBuilder();
        string? currentSection = null;

        foreach (var rawLine in template.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = rawLine.Trim();
            var header = trimmed.StartsWith("#[", StringComparison.Ordinal)
                ? trimmed[1..]
                : trimmed;
            if (header.StartsWith('[') && header.EndsWith(']') && !header.StartsWith("[[", StringComparison.Ordinal))
            {
                currentSection = header[1..^1].Trim();
                if (sections.ContainsKey(currentSection))
                {
                    seenSections.Add(currentSection);
                    seenEntries[currentSection] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    output.Append('[').Append(currentSection).AppendLine("]");
                    if (!templateEnableSections.Contains(currentSection))
                    {
                        output.Append("enable = ")
                            .AppendLine(values[currentSection].Enabled ? "true" : "false");
                        seenEntries[currentSection].Add("enable");
                    }
                    continue;
                }
                output.AppendLine(rawLine);
                continue;
            }

            if (currentSection != null && sections.TryGetValue(currentSection, out var section))
            {
                var line = trimmed.StartsWith('#') && !trimmed.StartsWith("##", StringComparison.Ordinal)
                    ? trimmed[1..].TrimStart()
                    : trimmed;
                var equals = line.IndexOf('=');
                if (equals > 0)
                {
                    var key = line[..equals].Trim();
                    if (string.Equals(key, "enable", StringComparison.OrdinalIgnoreCase))
                    {
                        output.Append("enable = ").AppendLine(values[currentSection].Enabled ? "true" : "false");
                        seenEntries[currentSection].Add("enable");
                        continue;
                    }

                    var entry = section.Entries.FirstOrDefault(item =>
                        string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
                    if (entry != null)
                    {
                        output.Append(entry.Key).Append(" = ")
                            .AppendLine(FormatTomlValue(values[currentSection].Entries[entry.Key]));
                        seenEntries[currentSection].Add(entry.Key);
                        continue;
                    }
                }
            }

            output.AppendLine(rawLine);
        }

        foreach (var section in schema)
        {
            if (!seenSections.Contains(section.Id))
                throw new InvalidDataException($"默认配置模板缺少 [{section.Id}] section");
            if (!seenEntries[section.Id].Contains("enable"))
                throw new InvalidDataException($"默认配置模板缺少 {section.Id}.enable");
            foreach (var entry in section.Entries)
            {
                if (!seenEntries[section.Id].Contains(entry.Key))
                    throw new InvalidDataException($"默认配置模板缺少 {section.Id}.{entry.Key}");
            }
        }

        return output.ToString();
    }

    private static HashSet<string> FindTemplateEnableSections(
        string template,
        IReadOnlyDictionary<string, SectionSchema> sections)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;
        foreach (var rawLine in template.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = rawLine.Trim();
            var header = trimmed.StartsWith("#[", StringComparison.Ordinal)
                ? trimmed[1..]
                : trimmed;
            if (header.StartsWith('[') && header.EndsWith(']') && !header.StartsWith("[[", StringComparison.Ordinal))
            {
                currentSection = header[1..^1].Trim();
                continue;
            }

            if (currentSection == null || !sections.ContainsKey(currentSection))
                continue;
            var line = trimmed.StartsWith('#') && !trimmed.StartsWith("##", StringComparison.Ordinal)
                ? trimmed[1..].TrimStart()
                : trimmed;
            var equals = line.IndexOf('=');
            if (equals > 0 && string.Equals(line[..equals].Trim(), "enable", StringComparison.OrdinalIgnoreCase))
                result.Add(currentSection);
        }

        return result;
    }

    private static bool TryNormalizeValue(EntrySchema entry, object? value, out object? normalized)
    {
        if (!TryNormalizeValueType(entry, value, out normalized))
            return false;
        var normalizedValue = normalized;
        return entry.Options == null || entry.Options.Any(option => ValuesEqual(option, normalizedValue));
    }

    private static bool TryNormalizeValueType(EntrySchema entry, object? value, out object? normalized)
    {
        value = UnwrapJson(value);
        normalized = null;
        switch (entry.Type)
        {
            case "bool" when value is bool boolean:
                normalized = boolean;
                return true;
            case "string" when value is string text:
                normalized = text;
                return true;
            case "int" when TryReadInteger(value, out var integer)
                            && (!entry.Min.HasValue || integer >= entry.Min.Value)
                            && (!entry.Max.HasValue || integer <= entry.Max.Value):
                normalized = integer;
                return true;
            case "float" when TryReadDouble(value, out var number)
                              && (!entry.Min.HasValue || number >= entry.Min.Value)
                              && (!entry.Max.HasValue || number <= entry.Max.Value):
                normalized = number;
                return true;
            case "string_array" when value is IEnumerable<object?> objects:
            {
                var strings = new List<object?>();
                foreach (var item in objects)
                {
                    if (UnwrapJson(item) is not string stringValue)
                        return false;
                    strings.Add(stringValue);
                }
                normalized = strings;
                return true;
            }
            case "string_array" when value is IEnumerable<string> stringValues:
                normalized = stringValues.Cast<object?>().ToList();
                return true;
        }

        return false;
    }

    private static IReadOnlyList<object?>? ReadOptions(
        TomlTable rawEntry,
        string sectionId,
        EntrySchema entry)
    {
        if (!rawEntry.TryGetValue("options", out var rawOptions))
            return null;
        if (entry.Type is not ("string" or "int" or "float"))
            throw new InvalidDataException($"manifest 中 {sectionId}.{entry.Key} 的类型不支持 options");
        if (rawOptions is not IEnumerable<object?> optionItems)
            throw new InvalidDataException($"manifest 中 {sectionId}.{entry.Key}.options 必须是数组");

        var options = new List<object?>();
        foreach (var rawOption in optionItems)
        {
            if (rawOption is not TomlTable option
                || !option.TryGetValue("value", out var optionValue)
                || !TryNormalizeValueType(entry, optionValue, out var normalized))
            {
                throw new InvalidDataException(
                    $"manifest 中 {sectionId}.{entry.Key}.options 包含无效选项");
            }
            if (options.Any(existing => ValuesEqual(existing, normalized)))
                throw new InvalidDataException($"manifest 中 {sectionId}.{entry.Key}.options 包含重复值");
            options.Add(normalized);
        }

        if (options.Count == 0)
            throw new InvalidDataException($"manifest 中 {sectionId}.{entry.Key}.options 不能为空");
        return options;
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (left == null || right == null)
            return left == right;
        if (TryReadDouble(left, out var leftNumber) && TryReadDouble(right, out var rightNumber))
            return leftNumber.Equals(rightNumber);
        return Equals(left, right);
    }

    private static object? UnwrapJson(object? value)
    {
        if (value is not JsonElement element)
            return NormalizeTomlValue(value);
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.Array => element.EnumerateArray().Select(item => UnwrapJson(item)).ToList(),
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };
    }

    private static object? NormalizeTomlValue(object? value) => value switch
    {
        TomlArray array => array.Select(NormalizeTomlValue).ToList(),
        _ => value,
    };

    private static object? CloneValue(object? value) => value switch
    {
        List<object?> list => list.Select(CloneValue).ToList(),
        _ => value,
    };

    private static bool TryReadInteger(object? value, out long result)
    {
        try
        {
            if (value is byte or sbyte or short or ushort or int or uint or long)
            {
                result = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch (OverflowException)
        {
        }

        result = 0;
        return false;
    }

    private static bool TryReadDouble(object? value, out double result)
    {
        if (value is float or double or decimal || TryReadInteger(value, out _))
        {
            result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return double.IsFinite(result);
        }

        result = 0;
        return false;
    }

    private static long? ReadInteger(TomlTable table, string key)
    {
        return table.TryGetValue(key, out var value) && TryReadInteger(value, out var integer)
            ? integer
            : null;
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
        return document.TryGetValue(key, out var value) ? value as TomlTable : null;
    }

    private static string FormatTomlValue(object? value) => value switch
    {
        bool boolean => boolean ? "true" : "false",
        string text => $"\"{EscapeTomlString(text)}\"",
        byte or sbyte or short or ushort or int or uint or long or ulong =>
            Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
        float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
        IEnumerable<object?> values => $"[{string.Join(", ", values.Select(FormatTomlValue))}]",
        _ => throw new ArgumentException("无法写入不受支持的 TOML 值"),
    };

    private static string EscapeTomlString(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n")
        .Replace("\t", "\\t");

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
