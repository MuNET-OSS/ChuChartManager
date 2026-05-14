using System.Windows.Media;

namespace ChuChartManager;

public class DiffBadge
{
    public string Abbr { get; set; } = "";
    public string Level { get; set; } = "";
    public Brush BgBrush { get; set; } = Brushes.Gray;
    public Brush FgBrush { get; set; } = Brushes.White;
}
