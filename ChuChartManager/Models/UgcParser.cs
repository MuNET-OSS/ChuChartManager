using System.Globalization;

namespace ChuChartManager.Models;

public class UgcBeatEvent
{
    public int Measure { get; init; }
    public int Numerator { get; init; }
    public int Denominator { get; init; }
}

public class UgcSpeedEvent
{
    public int Measure { get; init; }
    public int Offset { get; init; }
    public double Multiplier { get; init; }
    public double Time { get; set; }
}

public class UgcChart
{
    public string Version { get; set; } = "";
    public string Title { get; set; } = "";
    public string SortTitle { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Designer { get; set; } = "";
    public int Difficulty { get; set; }
    public string Level { get; set; } = "";
    public double ChartConst { get; set; }
    public string SongId { get; set; } = "";
    public string Bgm { get; set; } = "";
    public double BgmOffset { get; set; }
    public string Jacket { get; set; } = "";
    public string BgImage { get; set; } = "";
    public int TicksPerBeat { get; set; } = 480;
    public int MainTil { get; set; }
    public Dictionary<string, string> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> AtInfo { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string[]>> HeaderDirectives { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<UgcBeatEvent> BeatEvents { get; } = [];
    public List<BpmEvent> BpmEvents { get; } = [];
    public List<UgcSpeedEvent> SpeedEvents { get; } = [];
    public List<ChartNote> Notes { get; } = [];

    public double TotalDuration { get; set; }
}

public static class UgcParser
{
    private sealed record ParsedPos(int Measure, int TickInMeasure, int AbsoluteTick);

    private sealed class PendingLong
    {
        public char StartType { get; init; }
        public int StartAbsTick { get; init; }
        public int StartCell { get; init; }
        public int StartWidth { get; init; }
        public int StartHeight { get; init; }
        public int StartMeasure { get; init; }
        public int StartOffset { get; init; }
        public List<DurationSeg> Segments { get; } = [];
    }

    private sealed class DurationSeg
    {
        public int CumulativeDuration { get; init; }
        public char SegmentType { get; init; }
        public int EndCell { get; init; }
        public int EndWidth { get; init; }
        public int TargetHeight { get; init; }
    }

    public static UgcChart Parse(string ugcText)
    {
        var chart = new UgcChart();
        var lines = SplitLines(ugcText);

        var bpmRaw = new List<(int measure, int tick, double bpm)>();
        var beatRaw = new List<UgcBeatEvent>();
        var speedRaw = new List<(int measure, int tick, double multiplier)>();
        var noteRaw = new List<string>();

        bool inHeader = true;
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('\''))
            {
                continue;
            }

            if (inHeader && line.StartsWith('@'))
            {
                var p = line.Split('\t');
                if (p.Length == 0)
                {
                    continue;
                }

                if (string.Equals(p[0], "@ENDHEAD", StringComparison.OrdinalIgnoreCase))
                {
                    inHeader = false;
                    continue;
                }

                ParseHeader(chart, p, bpmRaw, beatRaw, speedRaw);
                continue;
            }

            if (line.StartsWith('#'))
            {
                noteRaw.Add(line);
            }
        }

        if (beatRaw.Count == 0 || beatRaw.All(b => b.Measure != 0))
        {
            beatRaw.Add(new UgcBeatEvent { Measure = 0, Numerator = 4, Denominator = 4 });
        }

        beatRaw.Sort((a, b) => a.Measure.CompareTo(b.Measure));
        chart.BeatEvents.AddRange(beatRaw);

        int maxMeasure = 0;
        foreach (var (measure, _, _) in bpmRaw)
        {
            maxMeasure = Math.Max(maxMeasure, measure);
        }
        foreach (var (measure, _, _) in speedRaw)
        {
            maxMeasure = Math.Max(maxMeasure, measure);
        }
        foreach (var line in noteRaw)
        {
            if (TryGetStartMeasure(line, out var m))
            {
                maxMeasure = Math.Max(maxMeasure, m);
            }
        }

        var measureStarts = BuildMeasureStartTicks(chart.TicksPerBeat, beatRaw, maxMeasure + 16);

        foreach (var (measure, tick, bpm) in bpmRaw)
        {
            var abs = MeasureTickToAbsoluteTick(measure, tick, measureStarts);
            var (m, o) = AbsoluteTickToMeasureOffset(abs, measureStarts);
            chart.BpmEvents.Add(new BpmEvent
            {
                Measure = m,
                Offset = o,
                Bpm = bpm
            });
        }

        foreach (var (measure, tick, multiplier) in speedRaw)
        {
            var abs = MeasureTickToAbsoluteTick(measure, tick, measureStarts);
            var (m, o) = AbsoluteTickToMeasureOffset(abs, measureStarts);
            chart.SpeedEvents.Add(new UgcSpeedEvent
            {
                Measure = m,
                Offset = o,
                Multiplier = multiplier,
                Time = 0
            });
        }

        ParseNotes(chart, noteRaw, measureStarts);
        ComputeTimes(chart, measureStarts);
        chart.Notes.Sort((a, b) => a.Time.CompareTo(b.Time));
        return chart;
    }

    private static void ParseHeader(
        UgcChart chart,
        string[] p,
        List<(int measure, int tick, double bpm)> bpmRaw,
        List<UgcBeatEvent> beatRaw,
        List<(int measure, int tick, double multiplier)> speedRaw)
    {
        chart.HeaderDirectives.TryAdd(p[0], []);
        chart.HeaderDirectives[p[0]].Add(p[1..]);

        switch (p[0].ToUpperInvariant())
        {
            case "@VER":
                chart.Version = Str(p, 1);
                break;
            case "@TITLE":
                chart.Title = Str(p, 1);
                break;
            case "@SORT":
                chart.SortTitle = Str(p, 1);
                break;
            case "@ARTIST":
                chart.Artist = Str(p, 1);
                break;
            case "@DESIGN":
                chart.Designer = Str(p, 1);
                break;
            case "@DIFF":
                chart.Difficulty = Int(p, 1);
                break;
            case "@LEVEL":
                chart.Level = Str(p, 1);
                break;
            case "@CONST":
                chart.ChartConst = Dbl(p, 1);
                break;
            case "@SONGID":
                chart.SongId = Str(p, 1);
                break;
            case "@BGM":
                chart.Bgm = Str(p, 1);
                break;
            case "@BGMOFS":
                chart.BgmOffset = Dbl(p, 1);
                break;
            case "@JACKET":
                chart.Jacket = Str(p, 1);
                break;
            case "@BGIMG":
                chart.BgImage = Str(p, 1);
                break;
            case "@TICKS":
                chart.TicksPerBeat = Math.Max(1, Int(p, 1, 480));
                break;
            case "@MAINtil":
            case "@MAINTIL":
                chart.MainTil = Int(p, 1);
                break;
            case "@FLAG":
                if (p.Length >= 3)
                {
                    chart.Flags[p[1]] = p[2];
                }
                break;
            case "@ATINFO":
                if (p.Length >= 3)
                {
                    chart.AtInfo[p[1]] = p[2];
                }
                break;
            case "@BPM":
                {
                    var (m, t) = ParseMeasureTick(Str(p, 1));
                    bpmRaw.Add((m, t, Dbl(p, 2, 120.0)));
                    break;
                }
            case "@BEAT":
                {
                    var measure = Int(p, 1);
                    var num = Math.Max(1, Int(p, 2, 4));
                    var den = Math.Max(1, Int(p, 3, 4));
                    beatRaw.Add(new UgcBeatEvent { Measure = measure, Numerator = num, Denominator = den });
                    break;
                }
            case "@SPDMOD":
                {
                    var (m, t) = ParseMeasureTick(Str(p, 1));
                    speedRaw.Add((m, t, Dbl(p, 2, 1.0)));
                    break;
                }
        }
    }

    private static void ParseNotes(UgcChart chart, List<string> noteRaw, int[] measureStarts)
    {
        PendingLong? pending = null;

        foreach (var line in noteRaw)
        {
            if (TryParseStartLine(line, out var pos, out var data, measureStarts))
            {
                FlushPending(chart, ref pending, measureStarts);
                ParseStart(chart, pos, data, ref pending);
                continue;
            }

            if (TryParseDurationLine(line, out var seg))
            {
                if (pending != null)
                {
                    pending.Segments.Add(seg);
                }
            }
        }

        FlushPending(chart, ref pending, measureStarts);
    }

    private static void ParseStart(UgcChart chart, ParsedPos pos, string data, ref PendingLong? pending)
    {
        if (string.IsNullOrEmpty(data))
        {
            return;
        }

        char t = data[0];
        int cell = data.Length > 1 ? ParseCell(data[1]) : 0;
        int width = data.Length > 2 ? ParseWidth(data[2]) : 1;

        switch (t)
        {
            case 't':
                AddSimple(chart, NoteType.TAP, pos, cell, width);
                break;
            case 'd':
                AddSimple(chart, NoteType.MNE, pos, cell, width);
                break;
            case 'x':
                AddSimple(chart, NoteType.CHR, pos, cell, width, extra: ParseChrExtra(data));
                break;
            case 'f':
                AddSimple(chart, NoteType.FLK, pos, cell, width, extra: ParseFlkExtra(data));
                break;
            case 'a':
                AddSimple(chart, ParseAirType(data), pos, cell, width, target: ParseAirTarget(data));
                break;
            case 'h':
            case 's':
            case 'H':
            case 'C':
            case 'S':
                pending = new PendingLong
                {
                    StartType = t,
                    StartAbsTick = pos.AbsoluteTick,
                    StartCell = cell,
                    StartWidth = width,
                    StartHeight = data.Length > 3 ? ParseCell(data[3]) : 0,
                    StartMeasure = pos.Measure,
                    StartOffset = pos.TickInMeasure
                };
                break;
        }
    }

    private static void FlushPending(UgcChart chart, ref PendingLong? pending, int[] measureStarts)
    {
        if (pending == null)
        {
            return;
        }

        switch (pending.StartType)
        {
            case 'h':
                {
                    int dur = pending.Segments.Count > 0 ? Math.Max(0, pending.Segments[0].CumulativeDuration) : 0;
                    chart.Notes.Add(new ChartNote
                    {
                        Type = NoteType.HLD,
                        Measure = pending.StartMeasure,
                        Offset = pending.StartOffset,
                        Cell = pending.StartCell,
                        Width = pending.StartWidth,
                        HoldDuration = dur
                    });
                    break;
                }

            case 'H':
                {
                    int dur = pending.Segments.Count > 0 ? Math.Max(0, pending.Segments[0].CumulativeDuration) : 0;
                    chart.Notes.Add(new ChartNote
                    {
                        Type = NoteType.AHD,
                        Measure = pending.StartMeasure,
                        Offset = pending.StartOffset,
                        Cell = pending.StartCell,
                        Width = pending.StartWidth,
                        AirHoldDuration = dur,
                        TargetNote = "N"
                    });
                    break;
                }

            case 's':
                {
                    pending.Segments.Sort((a, b) => a.CumulativeDuration.CompareTo(b.CumulativeDuration));

                    int prevCum = 0;
                    int segStartAbs = pending.StartAbsTick;
                    int segStartCell = pending.StartCell;
                    int segStartWidth = pending.StartWidth;

                    foreach (var seg in pending.Segments)
                    {
                        int segDur = Math.Max(0, seg.CumulativeDuration - prevCum);
                        var (m, o) = AbsoluteTickToMeasureOffset(segStartAbs, measureStarts);
                        chart.Notes.Add(new ChartNote
                        {
                            Type = seg.SegmentType == 'c' ? NoteType.SLC : NoteType.SLD,
                            Measure = m,
                            Offset = o,
                            Cell = segStartCell,
                            Width = segStartWidth,
                            SlideDuration = segDur,
                            EndCell = seg.EndCell,
                            EndWidth = seg.EndWidth
                        });

                        segStartAbs += segDur;
                        segStartCell = seg.EndCell;
                        segStartWidth = seg.EndWidth;
                        prevCum = seg.CumulativeDuration;
                    }
                    break;
                }

            case 'C':
            case 'S':
                {
                    var first = pending.Segments.Count > 0
                        ? pending.Segments.OrderBy(s => s.CumulativeDuration).First()
                        : new DurationSeg
                        {
                            CumulativeDuration = 0,
                            SegmentType = 's',
                            EndCell = pending.StartCell,
                            EndWidth = pending.StartWidth,
                            TargetHeight = pending.StartHeight
                        };

                    chart.Notes.Add(new ChartNote
                    {
                        Type = pending.StartType == 'C' ? NoteType.ALD : NoteType.ASD,
                        Measure = pending.StartMeasure,
                        Offset = pending.StartOffset,
                        Cell = pending.StartCell,
                        Width = pending.StartWidth,
                        StartHeight = pending.StartHeight,
                        SlideDuration = Math.Max(0, first.CumulativeDuration),
                        EndCell = first.EndCell,
                        EndWidth = first.EndWidth,
                        TargetHeight = first.TargetHeight,
                        NoteColor = "0"
                    });
                    break;
                }
        }

        pending = null;
    }

    private static void AddSimple(
        UgcChart chart,
        NoteType type,
        ParsedPos pos,
        int cell,
        int width,
        string extra = "",
        string target = "")
    {
        chart.Notes.Add(new ChartNote
        {
            Type = type,
            Measure = pos.Measure,
            Offset = pos.TickInMeasure,
            Cell = cell,
            Width = width,
            Extra = extra,
            TargetNote = target
        });
    }

    private static void ComputeTimes(UgcChart chart, int[] measureStarts)
    {
        var bpmByTick = new List<(int absTick, BpmEvent ev)>();
        foreach (var ev in chart.BpmEvents)
        {
            int abs = MeasureTickToAbsoluteTick(ev.Measure, ev.Offset, measureStarts);
            bpmByTick.Add((abs, ev));
        }

        if (bpmByTick.Count == 0)
        {
            var ev = new BpmEvent { Measure = 0, Offset = 0, Bpm = 120.0, Time = 0 };
            chart.BpmEvents.Add(ev);
            bpmByTick.Add((0, ev));
        }

        bpmByTick.Sort((a, b) => a.absTick.CompareTo(b.absTick));
        if (bpmByTick[0].absTick > 0)
        {
            var head = new BpmEvent { Measure = 0, Offset = 0, Bpm = bpmByTick[0].ev.Bpm, Time = 0 };
            chart.BpmEvents.Add(head);
            bpmByTick.Insert(0, (0, head));
        }

        bpmByTick[0].ev.Time = 0;
        for (int i = 1; i < bpmByTick.Count; i++)
        {
            var prev = bpmByTick[i - 1];
            var curr = bpmByTick[i];
            int dtick = curr.absTick - prev.absTick;
            curr.ev.Time = prev.ev.Time + TickDeltaToSeconds(dtick, prev.ev.Bpm, chart.TicksPerBeat);
        }

        foreach (var spd in chart.SpeedEvents)
        {
            int abs = MeasureTickToAbsoluteTick(spd.Measure, spd.Offset, measureStarts);
            spd.Time = TickToTime(abs, bpmByTick, chart.TicksPerBeat);
        }

        double maxEnd = 0;
        foreach (var n in chart.Notes)
        {
            int start = MeasureTickToAbsoluteTick(n.Measure, n.Offset, measureStarts);
            n.Time = TickToTime(start, bpmByTick, chart.TicksPerBeat);

            int duration = n.HoldDuration > 0
                ? n.HoldDuration
                : n.SlideDuration > 0
                    ? n.SlideDuration
                    : n.AirHoldDuration;
            if (duration > 0)
            {
                n.EndTime = TickToTime(start + duration, bpmByTick, chart.TicksPerBeat);
            }
            else
            {
                n.EndTime = n.Time;
            }

            maxEnd = Math.Max(maxEnd, n.EndTime);
        }

        chart.TotalDuration = chart.Notes.Count > 0 ? maxEnd + 1.0 : 0;
    }

    private static double TickToTime(int absTick, List<(int absTick, BpmEvent ev)> bpmByTick, int ticksPerBeat)
    {
        var selected = bpmByTick[0];
        for (int i = bpmByTick.Count - 1; i >= 0; i--)
        {
            if (bpmByTick[i].absTick <= absTick)
            {
                selected = bpmByTick[i];
                break;
            }
        }

        return selected.ev.Time + TickDeltaToSeconds(absTick - selected.absTick, selected.ev.Bpm, ticksPerBeat);
    }

    private static double TickDeltaToSeconds(int tickDelta, double bpm, int ticksPerBeat)
        => tickDelta * 60.0 / (Math.Max(1e-9, bpm) * Math.Max(1, ticksPerBeat));

    private static int[] BuildMeasureStartTicks(int ticksPerBeat, List<UgcBeatEvent> beatEvents, int maxMeasure)
    {
        var starts = new int[Math.Max(2, maxMeasure + 2)];
        beatEvents.Sort((a, b) => a.Measure.CompareTo(b.Measure));

        int evIndex = 0;
        int num = beatEvents[0].Numerator;
        int den = beatEvents[0].Denominator;

        starts[0] = 0;
        for (int m = 0; m < starts.Length - 1; m++)
        {
            while (evIndex + 1 < beatEvents.Count && beatEvents[evIndex + 1].Measure <= m)
            {
                evIndex++;
                num = beatEvents[evIndex].Numerator;
                den = beatEvents[evIndex].Denominator;
            }

            int ticksPerMeasure = ComputeMeasureTicks(ticksPerBeat, num, den);
            starts[m + 1] = starts[m] + ticksPerMeasure;
        }

        return starts;
    }

    private static int ComputeMeasureTicks(int ticksPerBeat, int numerator, int denominator)
    {
        double v = ticksPerBeat * numerator * 4.0 / Math.Max(1, denominator);
        return Math.Max(1, (int)Math.Round(v));
    }

    private static int MeasureTickToAbsoluteTick(int measure, int tickInMeasure, int[] measureStarts)
    {
        if (measure < 0)
        {
            measure = 0;
        }

        if (measure >= measureStarts.Length - 1)
        {
            return measureStarts[^1] + tickInMeasure;
        }

        return measureStarts[measure] + tickInMeasure;
    }

    private static (int measure, int offset) AbsoluteTickToMeasureOffset(int absoluteTick, int[] measureStarts)
    {
        if (absoluteTick <= 0)
        {
            return (0, 0);
        }

        int lo = 0;
        int hi = measureStarts.Length - 1;
        while (lo + 1 < hi)
        {
            int mid = (lo + hi) / 2;
            if (measureStarts[mid] <= absoluteTick)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return (lo, absoluteTick - measureStarts[lo]);
    }

    private static bool TryParseStartLine(string line, out ParsedPos pos, out string data, int[] measureStarts)
    {
        pos = new ParsedPos(0, 0, 0);
        data = "";

        int colon = line.IndexOf(':');
        if (colon < 0) return false;

        var left = line[1..colon];
        data = colon + 1 < line.Length ? line[(colon + 1)..] : "";

        var (m, t) = ParseMeasureTick(left);
        int abs = MeasureTickToAbsoluteTick(m, t, measureStarts);
        pos = new ParsedPos(m, t, abs);
        return true;
    }

    private static bool TryParseDurationLine(string line, out DurationSeg seg)
    {
        seg = new DurationSeg();
        int arrow = line.IndexOf('>');
        if (arrow <= 1) return false;

        int dur = IntFromString(line[1..arrow]);
        var data = arrow + 1 < line.Length ? line[(arrow + 1)..] : "";
        if (data.Length == 0) return false;

        seg = new DurationSeg
        {
            CumulativeDuration = Math.Max(0, dur),
            SegmentType = data[0],
            EndCell = data.Length > 1 ? ParseCell(data[1]) : 0,
            EndWidth = data.Length > 2 ? ParseWidth(data[2]) : 1,
            TargetHeight = data.Length > 3 ? ParseCell(data[3]) : 0
        };
        return true;
    }

    private static string ParseChrExtra(string data)
    {
        char dir = data.Length > 3 ? char.ToUpperInvariant(data[3]) : 'U';
        return dir switch
        {
            'U' => "UP",
            'D' => "DW",
            'C' => "CE",
            'L' => "L",
            'R' => "R",
            _ => "UP"
        };
    }

    private static string ParseFlkExtra(string data)
    {
        char dir = data.Length > 3 ? char.ToUpperInvariant(data[3]) : 'A';
        return dir switch
        {
            'L' => "L",
            'R' => "R",
            _ => "A"
        };
    }

    private static NoteType ParseAirType(string data)
    {
        string d = data.Length >= 5 ? data.Substring(3, 2).ToUpperInvariant() : "UC";
        return d switch
        {
            "UR" => NoteType.AUR,
            "UL" => NoteType.AUL,
            "DC" => NoteType.ADW,
            "DR" => NoteType.ADR,
            "DL" => NoteType.ADL,
            _ => NoteType.AIR
        };
    }

    private static string ParseAirTarget(string data)
    {
        return data.Length > 5 ? data[5].ToString() : "N";
    }

    private static (int measure, int tick) ParseMeasureTick(string s)
    {
        int q = s.IndexOf('\'');
        if (q < 0)
        {
            return (IntFromString(s), 0);
        }

        int measure = IntFromString(s[..q]);
        int tick = q + 1 < s.Length ? IntFromString(s[(q + 1)..]) : 0;
        return (measure, Math.Max(0, tick));
    }

    private static int ParseCell(char c)
    {
        c = char.ToUpperInvariant(c);
        if (c is >= '0' and <= '9') return c - '0';
        if (c is >= 'A' and <= 'F') return c - 'A' + 10;
        return 0;
    }

    private static int ParseWidth(char c)
    {
        c = char.ToUpperInvariant(c);
        if (c is >= '1' and <= '9') return c - '0';
        if (c is >= 'A' and <= 'G') return c - 'A' + 10;
        return 1;
    }

    private static string[] SplitLines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static bool TryGetStartMeasure(string line, out int measure)
    {
        measure = 0;
        int colon = line.IndexOf(':');
        if (colon <= 1) return false;
        var left = line[1..colon];
        var (m, _) = ParseMeasureTick(left);
        measure = m;
        return true;
    }

    private static int Int(string[] p, int i, int def = 0)
        => i < p.Length && int.TryParse(p[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;

    private static int IntFromString(string s)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static double Dbl(string[] p, int i, double def = 0)
        => i < p.Length && double.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;

    private static string Str(string[] p, int i) => i < p.Length ? p[i] : "";
}
