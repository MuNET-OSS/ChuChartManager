using System.Globalization;
using System.Text;

namespace ChuChartManager.Models;

public class UgcConvertOptions
{
    public string Title { get; set; } = "Untitled";
    public string SortTitle { get; set; } = "";
    public string Artist { get; set; } = "Unknown";
    public string Designer { get; set; } = "";
    public string SongId { get; set; } = "";
    /// <summary>0=BAS, 1=ADV, 2=EXP, 3=MAS, 4=ULT</summary>
    public int Difficulty { get; set; } = 3;
    public string Level { get; set; } = "1";
    public double ChartConst { get; set; }
    public string? BgmFileName { get; set; }
    public double BgmOffset { get; set; }
    public string? JacketFileName { get; set; }
}

public static class UgcConverter
{
    private const int UgcTicksPerBeat = 480;
    private const int BeatsPerBar = 4;
    private const int UgcTicksPerMeasure = UgcTicksPerBeat * BeatsPerBar;

    public static string Convert(C2sChart chart, UgcConvertOptions options)
    {
        var sb = new StringBuilder();
        int scale = UgcTicksPerMeasure / chart.Resolution;

        WriteHeader(sb, chart, options, scale);
        WriteNotes(sb, chart, scale);
        return sb.ToString();
    }

    private static void WriteHeader(StringBuilder sb, C2sChart chart, UgcConvertOptions opt, int scale)
    {
        sb.AppendLine($"' Converted from C2S by ChuChartManager");
        sb.AppendLine($"@VER\t6");
        sb.AppendLine($"@TITLE\t{opt.Title}");
        sb.AppendLine($"@SORT\t{(string.IsNullOrEmpty(opt.SortTitle) ? opt.Title.ToUpperInvariant() : opt.SortTitle)}");
        sb.AppendLine($"@ARTIST\t{opt.Artist}");
        sb.AppendLine($"@DESIGN\t{(string.IsNullOrEmpty(opt.Designer) ? chart.Creator : opt.Designer)}");
        sb.AppendLine($"@DIFF\t{opt.Difficulty}");
        sb.AppendLine($"@LEVEL\t{opt.Level}");
        sb.AppendLine($"@CONST\t{opt.ChartConst:F5}");
        sb.AppendLine($"@SONGID\t{opt.SongId}");
        sb.AppendLine($"@BGM\t{opt.BgmFileName ?? ""}");
        sb.AppendLine($"@BGMOFS\t{opt.BgmOffset:F5}");
        sb.AppendLine($"@BGMPRV\t0.00000\t0.00000");
        sb.AppendLine($"@JACKET\t{opt.JacketFileName ?? ""}");
        sb.AppendLine($"@BGIMG\t");
        sb.AppendLine($"@BGMODE\tPASSIVE\tFALSE");
        sb.AppendLine($"@FLDCOL\t0");
        sb.AppendLine($"@FLDIMG\t");
        sb.AppendLine($"@FLAG\tDIFFTTL\tFALSE");
        sb.AppendLine($"@FLAG\tSOFFSET\tTRUE");
        sb.AppendLine($"@FLAG\tCLICK\tTRUE");
        sb.AppendLine($"@FLAG\tEXLONG\tFALSE");
        sb.AppendLine($"@FLAG\tBGMWCMP\tTRUE");
        sb.AppendLine($"@ATINFO\tAUTHORS\t");
        sb.AppendLine($"@ATINFO\tSITES\t");
        sb.AppendLine($"@DLURL\t");
        sb.AppendLine($"@COPYRIGHT\t");
        sb.AppendLine($"@LICENSE\t\t");
        sb.AppendLine($"@TICKS\t{UgcTicksPerBeat}");
        sb.AppendLine($"@BEAT\t0\t{BeatsPerBar}\t{BeatsPerBar}");

        foreach (var bpm in chart.BpmEvents)
        {
            int ugcTick = bpm.Offset * scale;
            sb.AppendLine($"@BPM\t{bpm.Measure}'{ugcTick}\t{bpm.Bpm:F5}");
        }

        sb.AppendLine($"@TIL\t0\t0'0\t1.00000");

        foreach (var sfl in chart.SflEvents)
        {
            int startTick = sfl.Offset * scale;
            int endTick = (sfl.Offset + sfl.Duration) * scale;
            int endMeasure = sfl.Measure;

            while (startTick >= UgcTicksPerMeasure)
            {
                startTick -= UgcTicksPerMeasure;
            }

            sb.AppendLine($"@SPDMOD\t{sfl.Measure}'{startTick}\t{sfl.Multiplier:F5}");

            int endAbsTick = (sfl.Measure * chart.Resolution + sfl.Offset + sfl.Duration) * scale;
            int eMeasure = endAbsTick / UgcTicksPerMeasure;
            int eTick = endAbsTick % UgcTicksPerMeasure;
            sb.AppendLine($"@SPDMOD\t{eMeasure}'{eTick}\t1.00000");
        }

        sb.AppendLine($"@MAINTIL\t0");
        sb.AppendLine($"@ENDHEAD");
        sb.AppendLine();
    }

    private static void WriteNotes(StringBuilder sb, C2sChart chart, int scale)
    {
        int res = chart.Resolution;
        var notes = chart.Notes;

        var simpleNotes = new List<ChartNote>();
        var holdNotes = new List<ChartNote>();
        var slideNotes = new List<ChartNote>();
        var airNotes = new List<ChartNote>();
        var airHoldNotes = new List<ChartNote>();
        var airAdvNotes = new List<ChartNote>();

        foreach (var n in notes)
        {
            switch (n.Type)
            {
                case NoteType.TAP: case NoteType.MNE:
                    simpleNotes.Add(n);
                    break;
                case NoteType.CHR: case NoteType.FLK:
                    simpleNotes.Add(n);
                    break;
                case NoteType.HLD: case NoteType.HXD:
                    holdNotes.Add(n);
                    break;
                case NoteType.SLD: case NoteType.SLC:
                case NoteType.SXD: case NoteType.SXC:
                    slideNotes.Add(n);
                    break;
                case NoteType.AIR: case NoteType.AUR: case NoteType.AUL:
                case NoteType.ADW: case NoteType.ADR: case NoteType.ADL:
                    airNotes.Add(n);
                    break;
                case NoteType.AHD:
                    airHoldNotes.Add(n);
                    break;
                case NoteType.ALD: case NoteType.ASD:
                    airAdvNotes.Add(n);
                    break;
            }
        }

        // air 音符索引：(tick, cell) → 同位置的 air 音符列表
        var airIndex = new Dictionary<(int tick, int cell), List<ChartNote>>();
        foreach (var a in airNotes)
        {
            var key = (a.TotalTick(res), a.Cell);
            if (!airIndex.TryGetValue(key, out var list))
            {
                list = [];
                airIndex[key] = list;
            }
            list.Add(a);
        }

        var output = new List<UgcOutputItem>();
        int seq = 0;

        foreach (var n in simpleNotes)
        {
            int absTick = n.TotalTick(res) * scale;
            int m = absTick / UgcTicksPerMeasure;
            int t = absTick % UgcTicksPerMeasure;
            string noteStr = EncodeSimpleNote(n);
            output.Add(new UgcOutputItem(absTick, seq++, $"#{m}'{t}:{noteStr}"));

            WriteAttachedAir(output, ref seq, airIndex, n, res, scale, m, t);
        }

        foreach (var n in holdNotes)
        {
            int absTick = n.TotalTick(res) * scale;
            int m = absTick / UgcTicksPerMeasure;
            int t = absTick % UgcTicksPerMeasure;
            int dur = n.HoldDuration * scale;

            output.Add(new UgcOutputItem(absTick, seq++, $"#{m}'{t}:h{Hex(n.Cell)}{WHex(n.Width)}"));
            output.Add(new UgcOutputItem(absTick, seq++, $"#{dur}>s"));

            WriteAttachedAir(output, ref seq, airIndex, n, res, scale, m, t);
            WriteAirAtPosition(output, ref seq, airIndex,
                n.TotalTick(res) + n.HoldDuration, n.Cell, scale);
        }

        var chains = BuildSlideChains(slideNotes, res);
        foreach (var chain in chains)
        {
            var first = chain[0];
            int chainStartTick = first.TotalTick(res) * scale;
            int m = chainStartTick / UgcTicksPerMeasure;
            int t = chainStartTick % UgcTicksPerMeasure;

            output.Add(new UgcOutputItem(chainStartTick, seq++, $"#{m}'{t}:s{Hex(first.Cell)}{WHex(first.Width)}"));

            int accumDur = 0;
            foreach (var seg in chain)
            {
                accumDur += seg.SlideDuration * scale;
                char cType = (seg.Type is NoteType.SLC or NoteType.SXC) ? 'c' : 's';
                output.Add(new UgcOutputItem(chainStartTick, seq++, $"#{accumDur}>{cType}{Hex(seg.EndCell)}{WHex(seg.EndWidth)}"));
            }

            WriteAttachedAir(output, ref seq, airIndex, first, res, scale, m, t);
            var last = chain[^1];
            WriteAirAtPosition(output, ref seq, airIndex,
                last.TotalTick(res) + last.SlideDuration, last.EndCell, scale);
        }

        foreach (var n in airHoldNotes)
        {
            int absTick = n.TotalTick(res) * scale;
            int m = absTick / UgcTicksPerMeasure;
            int t = absTick % UgcTicksPerMeasure;
            int dur = n.AirHoldDuration * scale;

            output.Add(new UgcOutputItem(absTick, seq++, $"#{m}'{t}:H{Hex(n.Cell)}{WHex(n.Width)}0N"));
            output.Add(new UgcOutputItem(absTick, seq++, $"#{dur}>s"));
        }

        // ALD/ASD → AIR-CRUSH/AIR-SLIDE: 简化处理，暂不支持完整的空中滑键链
        foreach (var n in airAdvNotes)
        {
            int absTick = n.TotalTick(res) * scale;
            int m = absTick / UgcTicksPerMeasure;
            int t = absTick % UgcTicksPerMeasure;
            int dur = n.SlideDuration * scale;
            char noteType = n.Type == NoteType.ALD ? 'C' : 'S';

            output.Add(new UgcOutputItem(absTick, seq++,
                $"#{m}'{t}:{noteType}{Hex(n.Cell)}{WHex(n.Width)}{Hex(n.StartHeight)}0"));
            if (dur > 0)
            {
                output.Add(new UgcOutputItem(absTick, seq++,
                    $"#{dur}>s{Hex(n.EndCell)}{WHex(n.EndWidth)}{Hex(n.TargetHeight)}"));
            }
        }

        output.Sort((a, b) =>
        {
            int cmp = a.Tick.CompareTo(b.Tick);
            return cmp != 0 ? cmp : a.Seq.CompareTo(b.Seq);
        });
        foreach (var item in output)
            sb.AppendLine(item.Text);
    }

    private static List<List<ChartNote>> BuildSlideChains(List<ChartNote> slideNotes, int res)
    {
        if (slideNotes.Count == 0) return [];

        var sorted = slideNotes.OrderBy(n => n.TotalTick(res)).ThenBy(n => n.Cell).ToList();

        var startIndex = new Dictionary<(int, int), List<ChartNote>>();
        foreach (var n in sorted)
        {
            var key = (n.TotalTick(res), n.Cell);
            if (!startIndex.TryGetValue(key, out var list))
            {
                list = [];
                startIndex[key] = list;
            }
            list.Add(n);
        }

        var used = new HashSet<ChartNote>();
        var chains = new List<List<ChartNote>>();

        foreach (var n in sorted)
        {
            if (used.Contains(n)) continue;

            var chain = new List<ChartNote>();
            var current = n;

            while (current != null && !used.Contains(current))
            {
                used.Add(current);
                chain.Add(current);

                int endTick = current.TotalTick(res) + current.SlideDuration;
                int endCell = current.EndCell;
                var nextKey = (endTick, endCell);

                ChartNote? next = null;
                if (startIndex.TryGetValue(nextKey, out var candidates))
                {
                    next = candidates.FirstOrDefault(c => !used.Contains(c) && c.Width == current.EndWidth)
                        ?? candidates.FirstOrDefault(c => !used.Contains(c));
                }
                current = next;
            }

            if (chain.Count > 0)
                chains.Add(chain);
        }

        return chains;
    }

    private static string EncodeSimpleNote(ChartNote n) => n.Type switch
    {
        NoteType.TAP => $"t{Hex(n.Cell)}{WHex(n.Width)}",
        NoteType.MNE => $"d{Hex(n.Cell)}{WHex(n.Width)}",
        NoteType.CHR => $"x{Hex(n.Cell)}{WHex(n.Width)}{MapChrDirection(n.Extra)}",
        NoteType.FLK => $"f{Hex(n.Cell)}{WHex(n.Width)}{MapFlkDirection(n.Extra)}",
        _ => $"t{Hex(n.Cell)}{WHex(n.Width)}"
    };

    private static string EncodeAirNote(ChartNote n) => n.Type switch
    {
        NoteType.AIR => $"a{Hex(n.Cell)}{WHex(n.Width)}UCN",
        NoteType.AUR => $"a{Hex(n.Cell)}{WHex(n.Width)}URN",
        NoteType.AUL => $"a{Hex(n.Cell)}{WHex(n.Width)}ULN",
        NoteType.ADW => $"a{Hex(n.Cell)}{WHex(n.Width)}DCN",
        NoteType.ADR => $"a{Hex(n.Cell)}{WHex(n.Width)}DRN",
        NoteType.ADL => $"a{Hex(n.Cell)}{WHex(n.Width)}DLN",
        _ => $"a{Hex(n.Cell)}{WHex(n.Width)}UCN"
    };

    private static void WriteAttachedAir(List<UgcOutputItem> output, ref int seq,
        Dictionary<(int, int), List<ChartNote>> airIndex,
        ChartNote parent, int res, int scale, int m, int t)
    {
        WriteAirAtPosition(output, ref seq, airIndex, parent.TotalTick(res), parent.Cell, scale);
    }

    private static void WriteAirAtPosition(List<UgcOutputItem> output, ref int seq,
        Dictionary<(int, int), List<ChartNote>> airIndex,
        int c2sTick, int cell, int scale)
    {
        var key = (c2sTick, cell);
        if (!airIndex.TryGetValue(key, out var airs)) return;

        int absTick = c2sTick * scale;
        int m = absTick / UgcTicksPerMeasure;
        int t = absTick % UgcTicksPerMeasure;
        foreach (var a in airs)
            output.Add(new UgcOutputItem(absTick, seq++, $"#{m}'{t}:{EncodeAirNote(a)}"));
    }

    private static string MapChrDirection(string extra) => extra.ToUpperInvariant() switch
    {
        "UP" => "U",
        "DW" => "D",
        "CE" => "C",
        "L"  => "L",
        "R"  => "R",
        _    => "U"
    };

    private static string MapFlkDirection(string extra) => extra.ToUpperInvariant() switch
    {
        "L" => "L",
        "R" => "R",
        _   => "A"
    };

    /// <summary>Cell: 0-15 → '0'-'F'</summary>
    private static char Hex(int v)
    {
        v = Math.Clamp(v, 0, 15);
        return v < 10 ? (char)('0' + v) : (char)('A' + v - 10);
    }

    /// <summary>Width: 1-16 → '1'-'G' (扩展 hex，G=16)</summary>
    private static char WHex(int w)
    {
        w = Math.Clamp(w, 1, 16);
        return w < 10 ? (char)('0' + w) : (char)('A' + w - 10);
    }

    private record UgcOutputItem(int Tick, int Seq, string Text);
}
