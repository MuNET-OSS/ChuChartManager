using System.Globalization;

namespace ChuChartManager.Models;

/// <summary>c2s 文件中的 Note 类型，与格式标识一一对应</summary>
public enum NoteType
{
    TAP, CHR, HLD, HXD,
    SLD, SLC, SXD, SXC,
    FLK,
    AIR, AUR, AUL,
    AHD,
    ADW, ADR, ADL,
    ALD, ASD,
    MNE
}

/// <summary>单个 Note 数据</summary>
public class ChartNote
{
    public NoteType Type { get; init; }
    public int Measure { get; init; }
    public int Offset { get; init; }
    public int Cell { get; init; }
    public int Width { get; init; }

    public double Time { get; set; }
    public double EndTime { get; set; }

    public int HoldDuration { get; init; }      // HLD / HXD
    public int SlideDuration { get; init; }     // SLD / SLC / SXD / SXC
    public int EndCell { get; init; }
    public int EndWidth { get; init; }
    public string Extra { get; init; } = "";    // CHR animation / FLK direction
    public string TargetNote { get; init; } = "";
    public int AirHoldDuration { get; init; }   // AHD
    public int StartHeight { get; init; }       // ALD / ASD
    public int TargetHeight { get; init; }
    public string NoteColor { get; init; } = "";

    public int TotalTick(int resolution) => Measure * resolution + Offset;

    public bool IsSlide => Type is NoteType.SLD or NoteType.SLC or NoteType.SXD or NoteType.SXC;
    public bool IsAirAction => Type is NoteType.AIR or NoteType.AUR or NoteType.AUL
                                    or NoteType.ADW or NoteType.ADR or NoteType.ADL;
    public bool IsHold => Type is NoteType.HLD or NoteType.HXD;
}

/// <summary>BPM 变速事件</summary>
public class BpmEvent
{
    public int Measure { get; init; }
    public int Offset { get; init; }
    public double Bpm { get; init; }
    public double Time { get; set; }

    public int TotalTick(int resolution) => Measure * resolution + Offset;
}

/// <summary>SFL 流速变化事件</summary>
public class SflEvent
{
    public int Measure { get; init; }
    public int Offset { get; init; }
    public int Duration { get; init; }
    public double Multiplier { get; init; }
    public double Time { get; set; }
    public double EndTime { get; set; }

    public int TotalTick(int resolution) => Measure * resolution + Offset;
}

/// <summary>解析并持有一个 .c2s 谱面的全部数据</summary>
public class C2sChart
{
    public string Version { get; private set; } = "";
    public string Creator { get; private set; } = "";
    public int Resolution { get; private set; } = 384;
    public double BpmDef { get; private set; }

    public List<BpmEvent> BpmEvents { get; } = [];
    public List<SflEvent> SflEvents { get; } = [];
    public List<ChartNote> Notes { get; } = [];

    public double TotalDuration { get; private set; }

    // ────── 解析入口 ──────

    public static C2sChart Parse(string filePath)
    {
        var chart = new C2sChart();
        foreach (var line in File.ReadAllLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var p = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 2) continue;

            switch (p[0])
            {
                case "VERSION":    chart.Version = p[1]; break;
                case "CREATOR":    chart.Creator = p.Length > 1 ? string.Join("\t", p[1..]) : ""; break;
                case "RESOLUTION": chart.Resolution = Int(p, 1, 384); break;
                case "BPM_DEF":    chart.BpmDef = Dbl(p, 1); break;
                case "BPM":        chart.ParseBpm(p); break;
                case "SFL":        chart.ParseSfl(p); break;

                case "TAP": case "CHR": case "HLD": case "HXD":
                case "SLD": case "SLC": case "SXD": case "SXC":
                case "FLK":
                case "AIR": case "AUR": case "AUL":
                case "AHD":
                case "ADW": case "ADR": case "ADL":
                case "ALD": case "ASD":
                case "MNE":
                    chart.ParseNote(p);
                    break;
            }
        }

        chart.ComputeTimes();
        return chart;
    }

    // ────── BPM / SFL 解析 ──────

    private void ParseBpm(string[] p)
    {
        if (p.Length < 4) return;
        BpmEvents.Add(new BpmEvent
        {
            Measure = Int(p, 1),
            Offset  = Int(p, 2),
            Bpm     = Dbl(p, 3)
        });
    }

    private void ParseSfl(string[] p)
    {
        if (p.Length < 5) return;
        SflEvents.Add(new SflEvent
        {
            Measure    = Int(p, 1),
            Offset     = Int(p, 2),
            Duration   = Int(p, 3),
            Multiplier = Dbl(p, 4)
        });
    }

    // ────── Note 解析 ──────

    private void ParseNote(string[] p)
    {
        if (p.Length < 5) return;
        if (!Enum.TryParse<NoteType>(p[0], out var type)) return;

        int m = Int(p, 1), o = Int(p, 2), c = Int(p, 3), w = Int(p, 4);

        switch (type)
        {
            case NoteType.TAP:
            case NoteType.MNE:
                Add(type, m, o, c, w);
                break;

            case NoteType.CHR:
                Add(type, m, o, c, w, extra: Str(p, 5));
                break;

            case NoteType.FLK:
                Add(type, m, o, c, w, extra: Str(p, 5));
                break;

            case NoteType.HLD:
            case NoteType.HXD:
                Add(type, m, o, c, w, holdDur: Int(p, 5), extra: Str(p, 6));
                break;

            case NoteType.SLD:
            case NoteType.SLC:
            case NoteType.SXD:
            case NoteType.SXC:
                Add(type, m, o, c, w,
                    slideDur: Int(p, 5), endCell: Int(p, 6), endWidth: Int(p, 7),
                    extra: Str(p, 8));
                break;

            case NoteType.AIR:
            case NoteType.AUR:
            case NoteType.AUL:
            case NoteType.ADW:
            case NoteType.ADR:
            case NoteType.ADL:
                Add(type, m, o, c, w, target: Str(p, 5));
                break;

            case NoteType.AHD:
                Add(type, m, o, c, w, target: Str(p, 5), airHoldDur: Int(p, 6));
                break;

            case NoteType.ALD:
            case NoteType.ASD:
                if (p.Length >= 12)
                {
                    Notes.Add(new ChartNote
                    {
                        Type = type, Measure = m, Offset = o, Cell = c, Width = w,
                        TargetNote   = Str(p, 5),
                        StartHeight  = Int(p, 6),
                        SlideDuration = Int(p, 7),
                        EndCell      = Int(p, 8),
                        EndWidth     = Int(p, 9),
                        TargetHeight = Int(p, 10),
                        NoteColor    = Str(p, 11)
                    });
                }
                break;
        }
    }

    private void Add(NoteType type, int m, int o, int c, int w,
        int holdDur = 0, int slideDur = 0, int endCell = 0, int endWidth = 0,
        string extra = "", string target = "", int airHoldDur = 0)
    {
        Notes.Add(new ChartNote
        {
            Type = type, Measure = m, Offset = o, Cell = c, Width = w,
            HoldDuration    = holdDur,
            SlideDuration   = slideDur,
            EndCell         = endCell,
            EndWidth        = endWidth,
            Extra           = extra,
            TargetNote      = target,
            AirHoldDuration = airHoldDur,
        });
    }

    // ────── 时间计算 ──────

    private void ComputeTimes()
    {
        // 确保至少有一个 BPM 事件
        if (BpmEvents.Count == 0)
            BpmEvents.Add(new BpmEvent { Measure = 0, Offset = 0, Bpm = BpmDef > 0 ? BpmDef : 120 });

        BpmEvents.Sort((a, b) => a.TotalTick(Resolution).CompareTo(b.TotalTick(Resolution)));

        // 计算每个 BPM 事件的绝对时间
        BpmEvents[0].Time = 0;
        for (int i = 1; i < BpmEvents.Count; i++)
        {
            var prev = BpmEvents[i - 1];
            var curr = BpmEvents[i];
            var tickDelta = curr.TotalTick(Resolution) - prev.TotalTick(Resolution);
            curr.Time = prev.Time + TickDeltaToSeconds(tickDelta, prev.Bpm);
        }

        // 计算 SFL 事件时间
        foreach (var sfl in SflEvents)
        {
            sfl.Time = TickToTime(sfl.TotalTick(Resolution));
            sfl.EndTime = TickToTime(sfl.TotalTick(Resolution) + sfl.Duration);
        }
        SflEvents.Sort((a, b) => a.Time.CompareTo(b.Time));

        // 计算每个 Note 的时间
        foreach (var n in Notes)
        {
            var tick = n.TotalTick(Resolution);
            n.Time = TickToTime(tick);

            if (n.HoldDuration > 0)
                n.EndTime = TickToTime(tick + n.HoldDuration);
            else if (n.SlideDuration > 0)
                n.EndTime = TickToTime(tick + n.SlideDuration);
            else if (n.AirHoldDuration > 0)
                n.EndTime = TickToTime(tick + n.AirHoldDuration);
        }

        Notes.Sort((a, b) => a.Time.CompareTo(b.Time));

        if (Notes.Count > 0)
            TotalDuration = Notes.Max(n => Math.Max(n.Time, n.EndTime)) + 1.0;
    }

    /// <summary>tick → 绝对秒数</summary>
    public double TickToTime(int tick)
    {
        var ev = BpmEvents[0];
        for (int i = BpmEvents.Count - 1; i >= 0; i--)
        {
            if (BpmEvents[i].TotalTick(Resolution) <= tick)
            {
                ev = BpmEvents[i];
                break;
            }
        }
        return ev.Time + TickDeltaToSeconds(tick - ev.TotalTick(Resolution), ev.Bpm);
    }

    /// <summary>绝对秒数 → tick（近似）</summary>
    public int TimeToTick(double time)
    {
        var ev = BpmEvents[0];
        for (int i = BpmEvents.Count - 1; i >= 0; i--)
        {
            if (BpmEvents[i].Time <= time)
            {
                ev = BpmEvents[i];
                break;
            }
        }
        var dt = time - ev.Time;
        return ev.TotalTick(Resolution) + (int)(dt * ev.Bpm * Resolution / 240.0);
    }

    /// <summary>获取指定时间点的 SFL 倍率</summary>
    public double GetSflMultiplier(double time)
    {
        for (int i = SflEvents.Count - 1; i >= 0; i--)
        {
            var sfl = SflEvents[i];
            if (time >= sfl.Time && time < sfl.EndTime)
                return sfl.Multiplier;
        }
        return 1.0;
    }

    private double TickDeltaToSeconds(int tickDelta, double bpm)
        => tickDelta * 240.0 / (bpm * Resolution);

    // ────── 辅助 ──────

    private static int Int(string[] p, int i, int def = 0)
        => i < p.Length && int.TryParse(p[i], out var v) ? v : def;

    private static double Dbl(string[] p, int i, double def = 0)
        => i < p.Length && double.TryParse(p[i], CultureInfo.InvariantCulture, out var v) ? v : def;

    private static string Str(string[] p, int i)
        => i < p.Length ? p[i] : "";
}
