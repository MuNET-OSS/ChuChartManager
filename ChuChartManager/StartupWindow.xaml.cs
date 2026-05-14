using System.Windows;

namespace ChuChartManager;

public partial class StartupWindow : Window
{
    private readonly Config _config;
    public string SelectedPath { get; private set; } = "";

    public StartupWindow()
    {
        _config = Config.Load();
        InitializeComponent();

        Loaded += (_, _) => DwmHelper.EnableMica(this);

        if (!string.IsNullOrEmpty(_config.GamePath) && Directory.Exists(_config.GamePath))
        {
            TxtPath.Text = _config.GamePath;
            ValidatePath(_config.GamePath);
        }
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择游戏根目录"
        };
        if (!string.IsNullOrEmpty(TxtPath.Text) && Directory.Exists(TxtPath.Text))
            dialog.InitialDirectory = TxtPath.Text;

        if (dialog.ShowDialog() != true) return;
        TxtPath.Text = dialog.FolderName;
        ValidatePath(dialog.FolderName);
    }

    private void ValidatePath(string path)
    {
        if (Directory.Exists(Path.Combine(path, "data")) && Directory.Exists(Path.Combine(path, "bin")))
        {
            BtnStart.IsEnabled = true;
            TxtHint.Text = "目录有效";
            TxtHint.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("AccentBrush");
            SelectedPath = path;
        }
        else
        {
            BtnStart.IsEnabled = false;
            TxtHint.Text = "未找到 bin 和 data 文件夹";
            TxtHint.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(220, 60, 60));
        }
    }

    private void OnStart(object sender, RoutedEventArgs e)
    {
        _config.GamePath = SelectedPath;
        _config.HistoryPaths.Add(SelectedPath);
        _config.Save();
        DialogResult = true;
        Close();
    }
}
