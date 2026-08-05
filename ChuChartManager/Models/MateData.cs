namespace ChuChartManager.Models;

public record MateAction(
    int Id,
    int Type,
    string Emote,
    bool HasVoice,
    bool HasLipSync,
    bool IsSpecialMotion,
    int DurationMs);

public record MateEntry(
    string Id,
    int NumericId,
    string Name,
    string AssetDir,
    bool HasThumbnail,
    long EmoteFileSize,
    IReadOnlyList<MateAction> Actions);

public record MateAsset(MateEntry Entry, string EmotePath, string? ThumbnailPath);
