using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace ChuChartManager;

public class DifficultyItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? prop = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

    public int DiffIndex { get; set; }
    public string DiffName { get; set; } = "";

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; Notify(); Notify(nameof(EditVisibility)); Notify(nameof(CardOpacity)); }
    }

    public string LevelText { get; set; } = "";
    public string DesignerText { get; set; } = "";
    public Brush AccentBrush { get; set; } = Brushes.Gray;
    public Brush FgBrush { get; set; } = Brushes.White;

    public Visibility EditVisibility => IsEnabled ? Visibility.Visible : Visibility.Collapsed;
    public double CardOpacity => IsEnabled ? 1.0 : 0.4;
}
