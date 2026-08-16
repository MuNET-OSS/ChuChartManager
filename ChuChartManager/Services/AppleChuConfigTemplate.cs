using System.Globalization;
using System.Text;

namespace ChuChartManager.Services;

internal static class AppleChuConfigTemplate
{
    internal static string Apply(
        string template,
        IReadOnlyList<AppleChuConfigSectionSchema> schema,
        IReadOnlyDictionary<string, AppleChuConfigService.SectionState> values)
    {
        var seenSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEntries = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var completedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new StringBuilder();
        AppleChuConfigSectionSchema? currentSection = null;

        foreach (var rawLine in template.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = rawLine.Trim();
            var section = FindSection(trimmed, schema);
            if (section != null)
            {
                currentSection = section;
                seenSections.Add(section.Id);
                seenEntries[section.Id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var state = values[section.Id];
                var commentEntries = ShouldCommentSection(section, state);
                if (commentEntries)
                    output.Append('#');
                output.Append('[').Append(section.Id).AppendLine("]");
                if (UsesDisabledFlag(section) && !state.Enabled)
                    output.AppendLine("Disabled = true");
                TryAppendConfiguredExtras(
                    output,
                    section,
                    state,
                    seenEntries,
                    completedSections,
                    commentEntries);
                continue;
            }

            if (currentSection != null
                && UsesDisabledFlag(currentSection)
                && IsDisabledEntry(trimmed))
            {
                continue;
            }

            var entry = currentSection == null ? null : FindEntry(trimmed, currentSection);
            if (currentSection != null && entry != null)
            {
                seenEntries[currentSection.Id].Add(entry.Key);
                var state = values[currentSection.Id];
                var commentEntries = ShouldCommentSection(currentSection, state);
                if (AppleChuConfigSchema.IsEnableEntry(entry))
                    output.Append(entry.Key).Append(" = ").AppendLine(state.Enabled ? "true" : "false");
                else if (TryGetConfiguredValue(state, entry.Key, out var value))
                    AppendEntryLine(output, $"{entry.Key} = {FormatTomlValue(value)}", commentEntries);
                else
                    AppendEntryLine(output, rawLine, commentEntries);
                TryAppendConfiguredExtras(
                    output,
                    currentSection,
                    state,
                    seenEntries,
                    completedSections,
                    commentEntries);
                continue;
            }

            output.AppendLine(rawLine);
        }

        foreach (var section in schema)
        {
            if (!seenSections.Contains(section.Id))
                throw new InvalidDataException($"默认配置模板缺少 [{section.Id}] section");
            var required = section.Entries.Where(entry => !entry.Advanced);
            foreach (var entry in required)
            {
                if (!seenEntries[section.Id].Contains(entry.Key))
                    throw new InvalidDataException($"默认配置模板缺少 {section.Id}.{entry.Key}");
            }
        }

        return output.ToString();
    }

    private static AppleChuConfigSectionSchema? FindSection(
        string trimmed,
        IReadOnlyList<AppleChuConfigSectionSchema> schema)
    {
        var header = trimmed.StartsWith("#[", StringComparison.Ordinal) ? trimmed[1..] : trimmed;
        if (header.StartsWith('[') && header.EndsWith(']')
                                   && !header.StartsWith("[[", StringComparison.Ordinal))
        {
            var id = header[1..^1].Trim();
            var match = schema.FirstOrDefault(item => AppleChuConfigSchema.KeysEqual(item.Id, id));
            if (match != null)
                return match;
        }

        return null;
    }

    private static AppleChuConfigEntrySchema? FindEntry(
        string trimmed,
        AppleChuConfigSectionSchema section)
    {
        var line = trimmed.StartsWith('#') && !trimmed.StartsWith("##", StringComparison.Ordinal)
            ? trimmed[1..].TrimStart()
            : trimmed;
        var equals = line.IndexOf('=');
        if (equals > 0)
        {
            var key = line[..equals].Trim();
            var match = section.Entries.FirstOrDefault(item => AppleChuConfigSchema.KeysEqual(item.Key, key));
            if (match != null)
                return match;
        }

        return null;
    }

    private static void TryAppendConfiguredExtras(
        StringBuilder output,
        AppleChuConfigSectionSchema section,
        AppleChuConfigService.SectionState state,
        IReadOnlyDictionary<string, HashSet<string>> seenEntries,
        HashSet<string> completedSections,
        bool commentEntries)
    {
        if (completedSections.Contains(section.Id))
            return;
        var seen = seenEntries[section.Id];
        if (section.Entries.Any(entry => !entry.Advanced && !seen.Contains(entry.Key)))
            return;

        foreach (var entry in section.Entries)
        {
            if (seen.Contains(entry.Key)
                || AppleChuConfigSchema.IsEnableEntry(entry)
                || !TryGetConfiguredValue(state, entry.Key, out var value))
                continue;
            if (entry.EmitComment && !string.IsNullOrWhiteSpace(entry.Comment))
                output.Append("## ").AppendLine(entry.Comment.Trim());
            AppendEntryLine(output, $"{entry.Key} = {FormatTomlValue(value)}", commentEntries);
            seen.Add(entry.Key);
        }
        completedSections.Add(section.Id);
    }

    private static bool TryGetConfiguredValue(
        AppleChuConfigService.SectionState state,
        string key,
        out object? value)
    {
        foreach (var (candidate, candidateValue) in state.Entries)
        {
            if (AppleChuConfigSchema.KeysEqual(candidate, key))
            {
                value = candidateValue;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool ShouldCommentSection(
        AppleChuConfigSectionSchema section,
        AppleChuConfigService.SectionState state) =>
        !section.AlwaysEnabled
        && !HasEnableEntry(section)
        && !section.DefaultEnabled
        && !state.Enabled;

    private static bool UsesDisabledFlag(AppleChuConfigSectionSchema section) =>
        !section.AlwaysEnabled && !HasEnableEntry(section) && section.DefaultEnabled;

    private static bool HasEnableEntry(AppleChuConfigSectionSchema section) =>
        section.Entries.Any(AppleChuConfigSchema.IsEnableEntry);

    private static bool IsDisabledEntry(string trimmed)
    {
        var line = trimmed.StartsWith('#') ? trimmed[1..].TrimStart() : trimmed;
        var equals = line.IndexOf('=');
        return equals > 0 && AppleChuConfigSchema.KeysEqual(line[..equals].Trim(), "Disabled");
    }

    private static void AppendEntryLine(StringBuilder output, string line, bool commentOut)
    {
        if (!commentOut || line.TrimStart().StartsWith('#'))
        {
            output.AppendLine(line);
            return;
        }

        var trimmed = line.TrimStart();
        output.Append(line.AsSpan(0, line.Length - trimmed.Length));
        output.Append('#').AppendLine(trimmed);
    }

    private static string FormatTomlValue(object? value) => value switch
    {
        bool boolean => boolean ? "true" : "false",
        string text => $"\"{EscapeTomlString(text)}\"",
        byte or sbyte or short or ushort or int or uint or long or ulong =>
            Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
        float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
        IEnumerable<object?> items => $"[{string.Join(", ", items.Select(FormatTomlValue))}]",
        _ => throw new ArgumentException("无法写入不受支持的 TOML 值"),
    };

    private static string EscapeTomlString(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n")
        .Replace("\t", "\\t");
}
