using ChuChartManager.Models;

namespace ChuChartManager;

public static class MusicValidator
{
    public const string NoAudio = "NoAudio";
    public const string NoJacket = "NoJacket";
    public const string NoEnabledFumen = "NoEnabledFumen";

    public static List<string> Validate(MusicXml music)
    {
        var problems = new List<string>();

        if (AudioHelper.FindAwbPath(music) == null)
            problems.Add(NoAudio);

        if (music.GetJacketFullPath() == null)
            problems.Add(NoJacket);

        if (!music.Fumens.Any(f => f is { Enable: true }))
            problems.Add(NoEnabledFumen);

        return problems;
    }
}
