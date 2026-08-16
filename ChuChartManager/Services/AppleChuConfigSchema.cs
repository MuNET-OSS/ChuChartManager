using System.Globalization;
using System.Text.Json;
using Tomlyn.Model;

namespace ChuChartManager.Services;

internal sealed record AppleChuConfigEntrySchema(
    string Key,
    string Type,
    object? Default,
    long? Min,
    long? Max,
    bool Advanced,
    bool EmitComment,
    string? Comment,
    IReadOnlyList<object?>? Options);

internal sealed record AppleChuConfigSectionSchema(
    string Id,
    bool DefaultEnabled,
    bool AlwaysEnabled,
    IReadOnlyList<AppleChuConfigEntrySchema> Entries);

internal static class AppleChuConfigSchema
{
    internal static IReadOnlyList<AppleChuConfigSectionSchema> Parse(
        AppleChuMetadataService.Metadata metadata)
    {
        var config = (TomlTable)metadata.Manifest["config"];
        var rawSections = (TomlTableArray)config["sections"];
        var result = new List<AppleChuConfigSectionSchema>(rawSections.Count);

        foreach (var rawSection in rawSections)
        {
            var id = (string)rawSection["id"];
            var entries = new List<AppleChuConfigEntrySchema>();
            if (rawSection.TryGetValue("entries", out var entriesValue)
                && entriesValue is TomlTableArray rawEntries)
            {
                foreach (var rawEntry in rawEntries)
                    entries.Add(ParseEntry(rawEntry, id));
            }
            var alwaysEnabled = ReadBooleanAlias(rawSection, id, false, "always_enabled", "alwaysEnabled");
            var defaultEnabled = ReadBooleanAlias(
                rawSection,
                id,
                alwaysEnabled,
                "default_enabled",
                "default_on",
                "defaultOn");
            result.Add(new AppleChuConfigSectionSchema(id, defaultEnabled, alwaysEnabled, entries));
        }

        return result;
    }

    internal static bool TryNormalize(
        AppleChuConfigEntrySchema entry,
        object? value,
        out object? normalized)
    {
        if (!TryNormalizeType(entry, value, out normalized))
            return false;
        var normalizedValue = normalized;
        return entry.Options == null || entry.Options.Any(option => ValuesEqual(option, normalizedValue));
    }

    internal static bool TryGetValue(TomlTable? table, string key, out object? value)
    {
        if (table != null)
        {
            foreach (var (candidate, candidateValue) in table)
            {
                if (KeysEqual(candidate, key))
                {
                    value = candidateValue;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    internal static bool KeysEqual(string left, string right) => string.Equals(
        left.Replace("_", "", StringComparison.Ordinal),
        right.Replace("_", "", StringComparison.Ordinal),
        StringComparison.OrdinalIgnoreCase);

    internal static bool IsEnableEntry(AppleChuConfigEntrySchema entry) => KeysEqual(entry.Key, "enable");

    private static bool ReadBooleanAlias(
        TomlTable table,
        string sectionId,
        bool fallback,
        params string[] keys)
    {
        bool? result = null;
        foreach (var key in keys)
        {
            if (!table.TryGetValue(key, out var value))
                continue;
            if (value is not bool boolean)
                throw new InvalidDataException($"manifest 中 {sectionId}.{key} 必须是布尔值");
            if (result.HasValue && result.Value != boolean)
                throw new InvalidDataException($"manifest 中 {sectionId} 的启用状态元数据存在冲突");
            result = boolean;
        }

        return result ?? fallback;
    }

    private static AppleChuConfigEntrySchema ParseEntry(TomlTable rawEntry, string sectionId)
    {
        var key = rawEntry.TryGetValue("key", out var keyValue) ? keyValue as string : null;
        var type = rawEntry.TryGetValue("type", out var typeValue) ? typeValue as string : null;
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(type))
            throw new InvalidDataException($"{sectionId} 包含无效的配置项元数据");
        if (!rawEntry.TryGetValue("default", out var defaultValue))
            throw new InvalidDataException($"manifest 缺少 {sectionId}.{key} 默认值");

        var entry = new AppleChuConfigEntrySchema(
            key,
            type,
            NormalizeTomlValue(defaultValue),
            ReadInteger(rawEntry, "min"),
            ReadInteger(rawEntry, "max"),
            ReadBoolean(rawEntry, "advanced"),
            !rawEntry.TryGetValue("emit_comment", out var emitComment) || emitComment is not false,
            ReadComment(rawEntry),
            null);
        if (!TryNormalize(entry, entry.Default, out _))
            throw new InvalidDataException($"manifest 中 {sectionId}.{key} 的默认值无效");
        entry = entry with { Options = ReadOptions(rawEntry, sectionId, entry) };
        if (!TryNormalize(entry, entry.Default, out _))
            throw new InvalidDataException($"manifest 中 {sectionId}.{key} 的默认值不在 options 内");
        return entry;
    }

    private static bool TryNormalizeType(
        AppleChuConfigEntrySchema entry,
        object? value,
        out object? normalized)
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
            case "string_array" when value is IEnumerable<object?> values:
            {
                var strings = new List<object?>();
                foreach (var item in values)
                {
                    if (UnwrapJson(item) is not string stringValue)
                        return false;
                    strings.Add(stringValue);
                }
                normalized = strings;
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<object?>? ReadOptions(
        TomlTable rawEntry,
        string sectionId,
        AppleChuConfigEntrySchema entry)
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
                || !TryNormalizeType(entry, optionValue, out var normalized))
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

    private static string? ReadComment(TomlTable rawEntry)
    {
        if (!rawEntry.TryGetValue("label", out var labelValue) || labelValue is not TomlTable label)
            return null;
        return label.TryGetValue("zh", out var zh) && zh is string chinese
            ? chinese
            : label.TryGetValue("en", out var en) ? en as string : null;
    }

    private static bool ReadBoolean(TomlTable table, string key) =>
        table.TryGetValue(key, out var value) && value is true;

    private static long? ReadInteger(TomlTable table, string key) =>
        table.TryGetValue(key, out var value) && TryReadInteger(value, out var integer) ? integer : null;

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

    private static bool ValuesEqual(object? left, object? right)
    {
        if (left == null || right == null)
            return left == right;
        if (TryReadDouble(left, out var leftNumber) && TryReadDouble(right, out var rightNumber))
            return leftNumber.Equals(rightNumber);
        return Equals(left, right);
    }

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
}
