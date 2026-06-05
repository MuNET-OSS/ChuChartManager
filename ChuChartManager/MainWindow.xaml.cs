using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ChuChartManager.Models;

namespace ChuChartManager;

public partial class MainWindow : Window
{
    private readonly Config _config;
    private MusicScanner? _scanner;
    private readonly ObservableCollection<MusicRow> _musicRows = [];
    private MusicXml? _currentMusic;
    private MusicXml? _playingMusic;
    private bool _updatingFields;
    private readonly AudioHelper _audio = new();
    private readonly DispatcherTimer _playerTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private bool _isDraggingProgress;
    private byte[]? _cachedWav;

    // 难度标签页
    private readonly DifficultyItem[] _diffItems = new DifficultyItem[6];
    private int _selectedDiffTab = -1;
    private readonly Button[] _diffTabButtons = new Button[6];

    private static readonly Color[] DiffTabColors =
    [
        (Color)ColorConverter.ConvertFromString("#28853E"),  // BASIC
        (Color)ColorConverter.ConvertFromString("#B87014"),  // ADVANCED
        (Color)ColorConverter.ConvertFromString("#B82828"),  // EXPERT
        (Color)ColorConverter.ConvertFromString("#7028B0"),  // MASTER
        (Color)ColorConverter.ConvertFromString("#1A1A1A"),  // ULTIMA
        (Color)ColorConverter.ConvertFromString("#888888"),  // WORLD'S END
    ];

    private static readonly string[] BadgeAbbrs = ["B", "A", "E", "M", "U", "W"];
    private static readonly string[] BadgeColors = ["#28853E", "#B87014", "#B82828", "#7028B0", "#1A1A1A", null!];
    private static readonly string[] BadgeFgColors = ["#FFF", "#FFF", "#FFF", "#FFF", "#FF4545", null!];
    private static readonly string[] DiffNames = ["BASIC", "ADVANCED", "EXPERT", "MASTER", "ULTIMA", "WORLD'S END"];

    private static readonly Geometry PlayGeometry = Geometry.Parse("M 0,0 L 14,7 L 0,14 Z");
    private static readonly Geometry PauseGeometry = Geometry.Parse("M 0,0 L 4,0 L 4,14 L 0,14 Z M 9,0 L 13,0 L 13,14 L 9,14 Z");

    private static Brush GetDiffBgBrush(int index)
    {
        if (index == 5)
            return CreateRainbowBrush();
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(BadgeColors[index]));
    }

    private static Brush GetDiffFgBrush(int index)
    {
        if (index == 5)
            return Brushes.White;
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(BadgeFgColors[index]));
    }

    private static LinearGradientBrush CreateRainbowBrush()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 60, 60), 0.0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 180, 0), 0.2));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(80, 220, 50), 0.4));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 180, 255), 0.6));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(120, 60, 255), 0.8));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(220, 50, 200), 1.0));
        brush.Freeze();
        return brush;
    }

    public MainWindow()
    {
        _config = Config.Load();
        InitializeComponent();
        MusicListBox.ItemsSource = _musicRows;

        _playerTimer.Tick += OnPlayerTimerTick;
        _audio.PlaybackEnded += OnPlaybackEnded;

        Loaded += (_, _) =>
        {
            DwmHelper.EnableMica(this);
            SliderVolume.Value = 80;
            LoadData();
        };

        Closed += (_, _) =>
        {
            _playerTimer.Stop();
            _audio.Dispose();
        };
    }

    private async void LoadData()
    {
        if (string.IsNullOrEmpty(_config.GamePath)) return;

        MusicListBox.IsEnabled = false;
        SourceSelector.IsEnabled = false;
        StatusText.Text = "正在扫描...";
        Title = "ChuChartManager - 正在扫描...";

        var scanner = new MusicScanner(_config.GamePath);
        await Task.Run(scanner.ScanAll);
        _scanner = scanner;

        if (_scanner.Errors.Count > 0)
        {
            MessageBox.Show(
                $"扫描完成，但有 {_scanner.Errors.Count} 个错误：\n\n{string.Join("\n", _scanner.Errors.Take(10))}",
                "扫描警告", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        SourceSelector.SelectionChanged -= SourceSelector_Changed;
        SourceSelector.Items.Clear();
        foreach (var src in _scanner.AvailableSources)
        {
            var count = _scanner.MusicBySource.TryGetValue(src, out var list) ? list.Count : 0;
            SourceSelector.Items.Add(new ComboBoxItem { Content = $"{src} ({count})", Tag = src });
        }
        if (SourceSelector.Items.Count > 0)
            SourceSelector.SelectedIndex = 0;
        SourceSelector.SelectionChanged += SourceSelector_Changed;

        GenreFilter.SelectionChanged -= GenreFilter_Changed;
        GenreFilter.Items.Clear();
        GenreFilter.Items.Add(new ComboBoxItem { Content = "全部流派", Tag = -1 });
        var genreMap = MusicScanner.BuildGenreMap(_scanner);
        foreach (var sort in GenreSortXml.ScanAll(_config.GamePath))
        foreach (var (id, name) in sort.Entries)
        {
            if (!genreMap.ContainsKey(id))
                genreMap[id] = string.IsNullOrWhiteSpace(name) ? $"Genre {id}" : name;
        }
        foreach (var (id, name) in genreMap)
            GenreFilter.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        GenreFilter.SelectedIndex = 0;
        GenreFilter.SelectionChanged += GenreFilter_Changed;

        DiffFilter.SelectionChanged -= DiffFilter_Changed;
        DiffFilter.Items.Clear();
        DiffFilter.Items.Add(new ComboBoxItem { Content = "全部难度", Tag = -1 });
        DiffFilter.Items.Add(new ComboBoxItem { Content = "含 ULTIMA", Tag = 4 });
        DiffFilter.Items.Add(new ComboBoxItem { Content = "WORLD'S END", Tag = 5 });
        DiffFilter.SelectedIndex = 0;
        DiffFilter.SelectionChanged += DiffFilter_Changed;

        PopulateCards(_scanner.AvailableSources.FirstOrDefault() ?? "A000");

        CmbGenre.Items.Clear();
        var genreMap = MusicScanner.BuildGenreMap(_scanner);
        foreach (var sort in GenreSortXml.ScanAll(_config.GamePath))
        foreach (var (id, name) in sort.Entries)
        {
            if (!genreMap.ContainsKey(id))
                genreMap[id] = string.IsNullOrWhiteSpace(name) ? $"Genre {id}" : name;
        }
        foreach (var (id, name) in genreMap)
            CmbGenre.Items.Add(new ComboBoxItem { Content = name, Tag = id });

        MusicListBox.IsEnabled = true;
        SourceSelector.IsEnabled = true;

        var totalCount = _scanner.MusicBySource.Values.Sum(l => l.Count);
        Title = $"ChuChartManager - {totalCount} 首曲目";
        StatusText.Text = $"共 {totalCount} 首  |  {_scanner.AvailableSources.Count} 个 option  |  {_config.GamePath}";
    }

    private void SourceSelector_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SourceSelector.SelectedItem is ComboBoxItem item && item.Tag is string src)
            PopulateCards(src);
    }

    private void GenreFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SourceSelector.SelectedItem is ComboBoxItem item && item.Tag is string src)
            PopulateCards(src);
    }

    private void DiffFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SourceSelector.SelectedItem is ComboBoxItem item && item.Tag is string src)
            PopulateCards(src);
    }

    private int GetSelectedGenreId()
    {
        if (GenreFilter.SelectedItem is ComboBoxItem { Tag: int id })
            return id;
        return -1;
    }

    private int GetSelectedDiffFilter()
    {
        if (DiffFilter.SelectedItem is ComboBoxItem { Tag: int id })
            return id;
        return -1;
    }

    private void PopulateCards(string source)
    {
        _musicRows.Clear();
        DetailPanel.Visibility = Visibility.Collapsed;
        _currentMusic = null;

        if (_scanner == null || !_scanner.MusicBySource.TryGetValue(source, out var list)) return;

        var genreId = GetSelectedGenreId();
        var diffId = GetSelectedDiffFilter();

        IEnumerable<Models.MusicXml> filtered = list;
        if (genreId >= 0)
            filtered = filtered.Where(m => m.GenreId == genreId);
        if (diffId >= 0)
            filtered = filtered.Where(m => m.Fumens[diffId] is { Enable: true });

        foreach (var m in filtered.OrderBy(x => x.Id))
        {
            var row = new MusicRow
            {
                Id = m.Id,
                IdDisplay = $"#{m.Id:D4}",
                Name = m.Name,
                Artist = m.Artist,
                Genre = string.Join(", ", m.Genres),
                Music = m
            };

            var jacketPath = m.GetJacketFullPath();
            if (jacketPath != null)
                row.JacketPath = jacketPath;

            for (int i = 0; i < 6; i++)
            {
                var f = m.Fumens[i];
                if (f is not { Enable: true }) continue;
                row.Badges.Add(new DiffBadge
                {
                    Abbr = BadgeAbbrs[i],
                    Level = i == 5 && !string.IsNullOrEmpty(m.WorldsEndTag) ? m.WorldsEndTag : f.LevelDisplay,
                    BgBrush = GetDiffBgBrush(i),
                    FgBrush = GetDiffFgBrush(i)
                });
            }

            _musicRows.Add(row);
        }

        var hasFilter = genreId >= 0 || diffId >= 0;
        StatusText.Text = hasFilter
            ? $"{source}: {_musicRows.Count}/{list.Count} 首曲目  |  {_config.GamePath}"
            : $"{source}: {list.Count} 首曲目  |  {_config.GamePath}";
    }

    private void MusicListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MusicListBox.SelectedItem is not MusicRow row || row.Music == null) return;
        ShowDetail(row.Music);
    }

    private void ShowDetail(MusicXml music)
    {
        _updatingFields = true;
        _currentMusic = music;

        TxtName.Text = music.Name;
        TxtArtist.Text = music.Artist;

        for (int i = 0; i < CmbGenre.Items.Count; i++)
        {
            if (CmbGenre.Items[i] is ComboBoxItem item && item.Tag is int gid && gid == music.GenreId)
            {
                CmbGenre.SelectedIndex = i;
                break;
            }
        }

        var bpm = music.GetBpmFromChart();
        TxtBpm.Text = bpm > 0 ? bpm.ToString("F0") : "";

        DetailPanel.Visibility = Visibility.Visible;
        BtnSave.Visibility = Visibility.Collapsed;

        LoadJacket(music);
        LoadDifficulties(music);

        _updatingFields = false;
    }

    private void OnFieldChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFields && _currentMusic != null)
            BtnSave.Visibility = Visibility.Visible;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_currentMusic == null) return;

        var root = _currentMusic.XmlDoc.SelectSingleNode("/MusicData");
        if (root == null) return;

        var nameNode = root.SelectSingleNode("name/str");
        if (nameNode != null) nameNode.InnerText = TxtName.Text;

        var artistNode = root.SelectSingleNode("artistName/str");
        if (artistNode != null) artistNode.InnerText = TxtArtist.Text;

        var genreStrNode = root.SelectSingleNode("genreNames/list/StringID/str");
        var genreIdNode = root.SelectSingleNode("genreNames/list/StringID/id");
        if (CmbGenre.SelectedItem is ComboBoxItem selectedGenre)
        {
            if (genreStrNode != null) genreStrNode.InnerText = selectedGenre.Content?.ToString() ?? "";
            if (genreIdNode != null && selectedGenre.Tag is int gid) genreIdNode.InnerText = gid.ToString();

            _currentMusic.Genres = [selectedGenre.Content?.ToString() ?? ""];
            _currentMusic.GenreId = selectedGenre.Tag is int id2 ? id2 : -1;
        }

        var fumenNodes = root.SelectNodes("fumens/MusicFumenData");
        if (fumenNodes != null)
        {
            SaveCurrentDiffFields();
            foreach (var diff in _diffItems)
            {
                if (diff.DiffIndex >= fumenNodes.Count) continue;
                var node = fumenNodes[diff.DiffIndex]!;

                var enableNode = node.SelectSingleNode("enable");
                if (enableNode != null) enableNode.InnerText = diff.IsEnabled.ToString().ToLower();

                var (level, dec) = ParseLevel(diff.LevelText);
                var levelNode = node.SelectSingleNode("level");
                if (levelNode != null) levelNode.InnerText = level.ToString();
                var decNode = node.SelectSingleNode("levelDecimal");
                if (decNode != null) decNode.InnerText = dec.ToString();

                var designerNode = node.SelectSingleNode("notesDesigner");
                if (designerNode != null) designerNode.InnerText = diff.DesignerText;

                if (_currentMusic.Fumens[diff.DiffIndex] != null)
                {
                    _currentMusic.Fumens[diff.DiffIndex].Enable = diff.IsEnabled;
                    _currentMusic.Fumens[diff.DiffIndex].Level = level;
                    _currentMusic.Fumens[diff.DiffIndex].LevelDecimal = dec;
                    _currentMusic.Fumens[diff.DiffIndex].NotesDesigner = diff.DesignerText;
                }
            }
        }

        _currentMusic.Name = TxtName.Text;
        _currentMusic.Artist = TxtArtist.Text;

        _currentMusic.Save();
        Log.Info($"保存: #{_currentMusic.Id:D4} {_currentMusic.Name}");
        BtnSave.Visibility = Visibility.Collapsed;
        StatusText.Text = $"已保存: {_currentMusic.Name}";

        if (MusicListBox.SelectedItem is MusicRow row)
        {
            row.Name = TxtName.Text;
            row.Artist = TxtArtist.Text;

            row.Badges.Clear();
            for (int i = 0; i < 6; i++)
            {
                var f = _currentMusic.Fumens[i];
                if (f is not { Enable: true }) continue;
                row.Badges.Add(new DiffBadge
                {
                    Abbr = BadgeAbbrs[i],
                    Level = i == 5 && !string.IsNullOrEmpty(_currentMusic.WorldsEndTag) ? _currentMusic.WorldsEndTag : f.LevelDisplay,
                    BgBrush = GetDiffBgBrush(i),
                    FgBrush = GetDiffFgBrush(i)
                });
            }

            var idx = _musicRows.IndexOf(row);
            if (idx >= 0)
            {
                _musicRows.RemoveAt(idx);
                _musicRows.Insert(idx, row);
                MusicListBox.SelectedIndex = idx;
            }
        }
    }

    private void OnPlayPause(object sender, RoutedEventArgs e)
    {
        if (!_audio.HasAudio) return;

        _audio.TogglePlayPause();
        UpdatePlayButton();
        if (_audio.IsPlaying) _playerTimer.Start();
        else _playerTimer.Stop();
    }

    private async Task PlayMusicAsync(MusicXml music)
    {
        try
        {
            StatusText.Text = "加载音频中...";
            BtnPlayPause.IsEnabled = false;

            var wav = await Task.Run(() => AudioHelper.GetWavFromMusic(music));
            if (wav == null)
            {
                StatusText.Text = "未找到音频文件";
                BtnPlayPause.IsEnabled = true;
                return;
            }

            _cachedWav = wav;
            _playingMusic = music;
            _audio.Play(wav);
            _audio.Volume = (float)(SliderVolume.Value / 100.0);

            TxtPlayerName.Text = music.Name;
            TxtPlayerArtist.Text = music.Artist;
            UpdatePlayerJacket(music);
            UpdatePlayButton();
            _playerTimer.Start();

            BtnPlayPause.IsEnabled = true;
            StatusText.Text = "";
        }
        catch (Exception ex)
        {
            BtnPlayPause.IsEnabled = true;
            StatusText.Text = $"播放失败: {ex.Message}";
        }
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        _audio.Stop();
        _playerTimer.Stop();
        _playingMusic = null;
        _cachedWav = null;

        UpdatePlayButton();
        SliderProgress.Value = 0;
        TxtCurrentTime.Text = "00:00";
        TxtTotalTime.Text = "00:00";
        TxtPlayerName.Text = "";
        TxtPlayerArtist.Text = "";
        PlayerJacket.Source = null;
    }

    private void OnPlaybackEnded(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _playerTimer.Stop();
            UpdatePlayButton();
            SliderProgress.Value = SliderProgress.Maximum;
            TxtCurrentTime.Text = FormatTime(_audio.TotalTime);
        });
    }

    private void UpdatePlayButton()
    {
        PlayPauseIcon.Data = _audio.IsPlaying ? PauseGeometry : PlayGeometry;
    }

    private void UpdatePlayerJacket(MusicXml music)
    {
        var path = music.GetJacketFullPath();
        if (path == null) { PlayerJacket.Source = null; return; }
        try { PlayerJacket.Source = LoadDdsAsBitmapSource(path); }
        catch { PlayerJacket.Source = null; }
    }

    private void OnPlayerTimerTick(object? sender, EventArgs e)
    {
        if (!_audio.HasAudio) return;

        if (!_audio.IsPlaying && !_audio.IsPaused)
        {
            _playerTimer.Stop();
            UpdatePlayButton();
            return;
        }

        if (!_isDraggingProgress)
        {
            var total = _audio.TotalTime.TotalSeconds;
            var current = _audio.CurrentTime.TotalSeconds;
            if (total > 0)
                SliderProgress.Value = current / total * 1000;
        }

        TxtCurrentTime.Text = FormatTime(_audio.CurrentTime);
        TxtTotalTime.Text = FormatTime(_audio.TotalTime);
    }

    private static string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}";

    private void OnProgressDragStart(object sender, MouseButtonEventArgs e)
    {
        _isDraggingProgress = true;
    }

    private void OnProgressDragEnd(object sender, MouseButtonEventArgs e)
    {
        _isDraggingProgress = false;
        if (!_audio.HasAudio) return;
        var ratio = SliderProgress.Value / 1000.0;
        _audio.Seek(TimeSpan.FromSeconds(ratio * _audio.TotalTime.TotalSeconds));
    }

    private void OnProgressChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDraggingProgress && _audio.HasAudio)
        {
            var ratio = SliderProgress.Value / 1000.0;
            var pos = TimeSpan.FromSeconds(ratio * _audio.TotalTime.TotalSeconds);
            TxtCurrentTime.Text = FormatTime(pos);
        }
    }

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _audio.Volume = (float)(SliderVolume.Value / 100.0);
    }

    private async void OnExportMp3(object sender, RoutedEventArgs e)
    {
        if (_currentMusic == null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"{_currentMusic.CueFileName}.mp3",
            Filter = "MP3 音频|*.mp3"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            StatusText.Text = "导出中...";
            var wav = (_playingMusic == _currentMusic && _cachedWav != null)
                ? _cachedWav
                : await Task.Run(() => AudioHelper.GetWavFromMusic(_currentMusic));

            if (wav == null) { StatusText.Text = "未找到音频文件"; return; }

            await Task.Run(() => AudioHelper.ExportMp3(wav, dlg.FileName));
            StatusText.Text = $"已导出: {dlg.FileName}";
        }
        catch (Exception ex) { StatusText.Text = $"导出失败: {ex.Message}"; }
    }

    private void LoadJacket(MusicXml music)
    {
        JacketImage.Source = null;
        var path = music.GetJacketFullPath();
        if (path == null) return;

        try
        {
            JacketImage.Source = LoadDdsAsBitmapSource(path);
        }
        catch { /* DDS 解码失败时忽略 */ }
    }

    private static BitmapSource? LoadDdsAsBitmapSource(string path, int decodeWidth = 0)
    {
        if (!path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(path);
            if (decodeWidth > 0) img.DecodePixelWidth = decodeWidth;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }

        using var pfimImage = Pfim.Pfimage.FromFile(path);
        if (pfimImage.Compressed) pfimImage.Decompress();

        var fmt = pfimImage.Format == Pfim.ImageFormat.Rgba32
            ? PixelFormats.Bgra32
            : PixelFormats.Bgr24;

        var bmp = BitmapSource.Create(
            pfimImage.Width, pfimImage.Height,
            96, 96, fmt, null,
            pfimImage.Data, pfimImage.Stride);
        bmp.Freeze();
        return bmp;
    }

    private void LoadDifficulties(MusicXml music)
    {
        DiffTabBar.Children.Clear();
        _selectedDiffTab = -1;

        int firstEnabled = 0;
        for (int i = 0; i < 6; i++)
        {
            var f = music.Fumens[i];
            var levelText = i == 5 && !string.IsNullOrEmpty(music.WorldsEndTag)
                ? music.WorldsEndTag
                : f?.LevelDetailDisplay ?? "0";

            _diffItems[i] = new DifficultyItem
            {
                DiffIndex = i,
                DiffName = DiffNames[i],
                IsEnabled = f is { Enable: true },
                LevelText = levelText,
                DesignerText = f?.NotesDesigner ?? "",
                AccentBrush = GetDiffBgBrush(i),
                FgBrush = GetDiffFgBrush(i)
            };

            if (_diffItems[i].IsEnabled && firstEnabled == 0)
                firstEnabled = i;

            var tabColor = DiffTabColors[i];
            var btn = new Button
            {
                Content = DiffNames[i],
                Tag = i,
                Foreground = i == 4 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4545")) : Brushes.White,
                Background = i == 5 ? CreateRainbowBrush() : MakeDiffBrush(tabColor, 0x88),
                Margin = new Thickness(0, 0, 2, 0),
                Style = (Style)FindResource("DiffTabButton"),
            };
            btn.Click += OnDiffTabClick;

            DiffTabBar.Children.Add(btn);
            _diffTabButtons[i] = btn;
        }

        SelectDiffTab(firstEnabled);
    }

    private static SolidColorBrush MakeDiffBrush(Color c, byte alpha)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        brush.Freeze();
        return brush;
    }

    private void OnDiffTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int index })
            SelectDiffTab(index);
    }

    private void SaveCurrentDiffFields()
    {
        if (_selectedDiffTab < 0 || _selectedDiffTab >= 6) return;
        var item = _diffItems[_selectedDiffTab];
        item.IsEnabled = ChkDiffEnabled.IsChecked == true;
        item.DesignerText = TxtDiffDesigner.Text;
        item.LevelText = TxtDiffLevel.Text;
    }

    private void SelectDiffTab(int index)
    {
        if (index == _selectedDiffTab) return;

        var wasUpdating = _updatingFields;
        _updatingFields = true;

        SaveCurrentDiffFields();
        _selectedDiffTab = index;

        for (int i = 0; i < 6; i++)
        {
            var selected = i == index;
            _diffTabButtons[i].FontWeight = selected ? FontWeights.Bold : FontWeights.Normal;

            if (i == 5)
            {
                _diffTabButtons[i].Background = CreateRainbowBrush();
                _diffTabButtons[i].Opacity = selected ? 1.0 : 0.5;
            }
            else
            {
                var c = DiffTabColors[i];
                _diffTabButtons[i].Background = MakeDiffBrush(c, selected ? (byte)0xEE : (byte)0x88);
                _diffTabButtons[i].Opacity = 1.0;
            }
        }

        var item = _diffItems[index];
        ChkDiffEnabled.IsChecked = item.IsEnabled;
        TxtDiffDesigner.Text = item.DesignerText;
        TxtDiffLevel.Text = item.LevelText;

        var fumen = _currentMusic?.Fumens[index];
        TxtDiffConst.Text = fumen != null ? fumen.LevelValue.ToString("F1") : "";
        TxtNoteCount.Text = "0";

        var accent = DiffTabColors[index];
        DiffDetailBorder.BorderBrush = MakeDiffBrush(accent, 0x80);
        DiffDetailBorder.BorderThickness = new Thickness(0, 2, 0, 0);
        DiffDetailBorder.Visibility = Visibility.Visible;

        _updatingFields = wasUpdating;
    }

    private void OnDiffTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFields && _currentMusic != null)
            BtnSave.Visibility = Visibility.Visible;
    }

    private void OnDiffCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_updatingFields && _currentMusic != null)
            BtnSave.Visibility = Visibility.Visible;
    }

    private void OnGenreChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingFields && _currentMusic != null)
            BtnSave.Visibility = Visibility.Visible;
    }

    private async void OnCardPlay(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not System.Windows.Controls.Button { Tag: MusicRow row } || row.Music == null) return;
        await PlayMusicAsync(row.Music);
    }

    private static (int level, int dec) ParseLevel(string text)
    {
        text = text.Trim();
        if (text.EndsWith('+'))
        {
            if (int.TryParse(text[..^1], out var lv))
                return (lv, 70);
        }
        if (text.Contains('.'))
        {
            var parts = text.Split('.');
            if (parts.Length == 2 && int.TryParse(parts[0], out var lv) && int.TryParse(parts[1], out var d))
                return (lv, d * 10);
        }
        if (int.TryParse(text, out var level))
            return (level, 0);
        return (0, 0);
    }


}
