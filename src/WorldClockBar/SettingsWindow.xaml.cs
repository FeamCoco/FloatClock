using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using WorldClockBar.Models;
using WorldClockBar.Services;
using Forms = System.Windows.Forms;

namespace WorldClockBar;

public partial class SettingsWindow : Window
{
    private const string DefaultFontValue = "Segoe UI Variable Display, Segoe UI";
    private const string DefaultFontLabel = "（默认）Segoe UI Variable Display";

    private static readonly string[] AlignKeys = { "Right", "Center", "Left" };

    private readonly SettingsService _settingsService;
    private readonly AutostartService _autostart;
    private readonly WindowPlacementService _placement;
    private readonly DispatcherTimer _persistTimer;
    private readonly DispatcherTimer _statusTimer;
    private readonly List<(string Title, string Sub, string Page)> _searchIndex;

    private AppSettings _s;
    private bool _loading = true;
    private bool _micaActive;

    public event Action<AppSettings>? SettingsChanged;

    public ObservableCollection<ClockRow> ClockRows { get; } = new();
    public List<TimeZoneItem> TimeZones { get; }

    public SettingsWindow(
        SettingsService settingsService,
        AppSettings current,
        AutostartService autostart,
        WindowPlacementService placement)
    {
        _settingsService = settingsService;
        _autostart = autostart;
        _placement = placement;
        _s = current;

        TimeZones = TimeDisplayService.GetSystemTimeZones()
            .Select(z => new TimeZoneItem(z.Id, z.DisplayName))
            .ToList();

        InitializeComponent();
        DataContext = this;
        CityList.ItemsSource = ClockRows;

        _persistTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _persistTimer.Tick += (_, _) => PersistNow();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
        _statusTimer.Tick += (_, _) => { StatusPill.Visibility = Visibility.Collapsed; _statusTimer.Stop(); };

        _searchIndex = BuildSearchIndex();

        VersionText.Text = "WorldClockBar " + VersionString();
        AboutVersion.Text = "版本 " + VersionString() + " · Windows 10 / 11 · .NET 8";
        ConfigPathText.Text = _settingsService.SettingsPath;

        ReloadClockRows();
        LoadAllFields();
        UpdateThemeSelection();
        _loading = false;
    }

    // ==================================================================
    //  Window chrome / theme
    // ==================================================================

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyBackdrop();
    }

    private void ApplyBackdrop()
    {
        // 方案二：整窗不透明。层次由不透明 token 阶梯（窗口→卡片→控件）表达，
        // 不再叠 Mica——半透明表面在背板未接管时会直接压在黑底上渲染成黑色块。
        _micaActive = false;
        var chrome = WindowChrome.GetWindowChrome(this);
        if (chrome is not null)
            chrome.GlassFrameThickness = new Thickness(0);
        SetResourceReference(BackgroundProperty, "Fluent.WindowBg");
    }

    /// <summary>Called by MainWindow when the OS theme flips.</summary>
    public void RefreshSystemTheme()
    {
        if (_micaActive)
            ApplyBackdrop();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseWindow(object sender, RoutedEventArgs e) => Close();

    private static string VersionString()
    {
        var v = typeof(SettingsWindow).Assembly.GetName().Version;
        return v is null ? "1.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    // ==================================================================
    //  Navigation
    // ==================================================================

    private void OnNavChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (NavClocks.IsChecked == true) ShowPage("clocks");
        else if (NavLook.IsChecked == true) ShowPage("look");
        else if (NavBehavior.IsChecked == true) ShowPage("behavior");
        else if (NavAbout.IsChecked == true) ShowPage("about");
    }

    private void ShowPage(string page)
    {
        PageClocks.Visibility = page == "clocks" ? Visibility.Visible : Visibility.Collapsed;
        PageLook.Visibility = page == "look" ? Visibility.Visible : Visibility.Collapsed;
        PageBehavior.Visibility = page == "behavior" ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = page == "about" ? Visibility.Visible : Visibility.Collapsed;
        SearchResultsHost.Visibility = Visibility.Collapsed;
        SearchResults.SelectedItem = null;
    }

    // ==================================================================
    //  Search
    // ==================================================================

    private sealed record SearchEntry(string Title, string Sub, string Page)
    {
        public override string ToString() => Title + "  —  " + Sub;
    }

    private List<(string Title, string Sub, string Page)> BuildSearchIndex() => new()
    {
        ("城市", "添加、删除、排序、更换时区", "clocks"),
        ("显示秒", "关闭后只显示时:分", "clocks"),
        ("时间格式", "12 / 24 小时制", "clocks"),
        ("贴边对齐", "默认停靠位置", "clocks"),
        ("贴回右下角", "恢复默认停靠位置", "clocks"),
        ("显示器", "时钟条所在的屏幕", "clocks"),
        ("主题预设", "跟随系统 / 品牌浅色 / 品牌深色 / 高对比", "look"),
        ("背景色", "时钟条背景（支持透明度）", "look"),
        ("文字色", "时分、城市名与秒数", "look"),
        ("字体", "时间与城市名共用", "look"),
        ("字号", "时间主体字号", "look"),
        ("条高", "时钟条整体高度", "look"),
        ("圆角", "时钟条圆角半径", "look"),
        ("透明度", "整体不透明度", "look"),
        ("开机自动启动", "写入启动项注册表", "behavior"),
        ("始终置顶", "置顶显示行为说明", "behavior"),
        ("项目主页", "GitHub 仓库", "about"),
        ("配置文件", "settings.json 位置", "about"),
        ("重置为默认配置", "恢复默认城市、主题与位置", "about"),
    };

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = SearchBox.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;

        var q = SearchBox.Text.Trim();
        if (q.Length == 0)
        {
            SearchResultsHost.Visibility = Visibility.Collapsed;
            SearchResults.ItemsSource = null;
            return;
        }

        var hits = _searchIndex
            .Where(x => x.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || x.Sub.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Select(x => new SearchEntry(x.Title, x.Sub, x.Page))
            .ToList();

        if (hits.Count == 0)
        {
            SearchResultsHost.Visibility = Visibility.Collapsed;
            SearchResults.ItemsSource = null;
            return;
        }

        SearchResults.ItemsSource = hits;
        SearchResultsHost.Visibility = Visibility.Visible;
    }

    private void OnSearchResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResults.SelectedItem is not SearchEntry entry)
            return;

        SearchBox.Clear();
        SearchResultsHost.Visibility = Visibility.Collapsed;

        switch (entry.Page)
        {
            case "clocks": NavClocks.IsChecked = true; break;
            case "look": NavLook.IsChecked = true; break;
            case "behavior": NavBehavior.IsChecked = true; break;
            case "about": NavAbout.IsChecked = true; break;
        }
    }

    // ==================================================================
    //  Cities
    // ==================================================================

    private void ReloadClockRows()
    {
        foreach (var row in ClockRows)
            row.PropertyChanged -= OnClockRowChanged;
        ClockRows.Clear();

        foreach (var c in _s.Clocks)
            AddRow(new ClockRow(c.Label, c.TimeZoneId, ResolveDisplay(c.TimeZoneId)));
    }

    private void AddRow(ClockRow row)
    {
        row.PropertyChanged += OnClockRowChanged;
        ClockRows.Add(row);
    }

    private void OnClockRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ClockRow.Label) or nameof(ClockRow.TimeZoneId))
            SchedulePersist();
    }

    private void OnAddCity(object sender, RoutedEventArgs e)
    {
        var tz = TimeZones.FirstOrDefault(t => t.Id == "China Standard Time") ?? TimeZones.FirstOrDefault();
        if (tz is null) return;
        AddRow(new ClockRow("新城市", tz.Id, tz.DisplayName));
        SchedulePersist();
    }

    private void OnRowMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not ClockRow row)
            return;

        var menu = new ContextMenu
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom,
            StaysOpen = false
        };

        var up = new MenuItem { Header = "上移", Icon = Glyph(E74A) };
        up.Click += (_, _) => MoveRow(row, -1);
        var down = new MenuItem { Header = "下移", Icon = Glyph(E74B) };
        down.Click += (_, _) => MoveRow(row, +1);
        var del = new MenuItem { Header = "删除", Icon = Glyph(E74D) };
        del.Click += (_, _) => DeleteRow(row);

        menu.Items.Add(up);
        menu.Items.Add(down);
        menu.Items.Add(new Separator());
        menu.Items.Add(del);
        menu.IsOpen = true;
    }

    private const string E74A = "\uE74A";
    private const string E74B = "\uE74B";
    private const string E74D = "\uE74D";

    private static TextBlock Glyph(string code) => new()
    {
        Text = code,
        FontFamily = (FontFamily)FindResource("Fluent.IconFont"),
        FontSize = 14
    };

    private static object FindResource(string key) => System.Windows.Application.Current.FindResource(key);

    private void MoveRow(ClockRow row, int delta)
    {
        var i = ClockRows.IndexOf(row);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= ClockRows.Count) return;
        ClockRows.Move(i, j);
        SchedulePersist();
    }

    private void DeleteRow(ClockRow row)
    {
        if (ClockRows.Count <= 1)
        {
            FlashStatus("至少保留一个城市", warn: true);
            return;
        }
        row.PropertyChanged -= OnClockRowChanged;
        ClockRows.Remove(row);
        SchedulePersist();
    }

    private void SyncClocksFromRows()
    {
        _s.Clocks = ClockRows
            .Select(r => new ClockEntry
            {
                Label = string.IsNullOrWhiteSpace(r.Label) ? "时钟" : r.Label.Trim(),
                TimeZoneId = string.IsNullOrWhiteSpace(r.TimeZoneId) ? "UTC" : r.TimeZoneId
            })
            .ToList();
    }

    private string ResolveDisplay(string id)
        => TimeZones.FirstOrDefault(t => t.Id == id)?.DisplayName ?? id;

    // ==================================================================
    //  Behavior: seconds + hour format
    // ==================================================================

    private void OnShowSecondsChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.Behavior.ShowSeconds = ShowSecondsSwitch.IsChecked == true;
        RebuildTimeFormat();
        SchedulePersist();
    }

    private void OnHourFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        RebuildTimeFormat();
        SchedulePersist();
    }

    private void RebuildTimeFormat()
    {
        var use24 = (HourFormatBox.SelectedItem as ComboBoxItem)?.Tag as string != "12";
        var baseFormat = use24 ? "HH:mm" : "h:mm tt";
        _s.Behavior.TimeFormat = _s.Behavior.ShowSeconds
            ? baseFormat.Replace("mm", "mm:ss")
            : baseFormat;
    }

    // ==================================================================
    //  Position + monitor
    // ==================================================================

    private void OnAlignChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var tag = (AlignBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Right";
        _s.Appearance.HorizontalAlignment = tag;
        _s.Appearance.FreePosition = false;
        UpdatePositionState();
        SchedulePersist();
    }

    private void OnRepositionClicked(object sender, RoutedEventArgs e)
    {
        _placement.ClearFreePosition(_s);
        _s.Appearance.HorizontalAlignment = "Right";
        _s.Appearance.OffsetX = 4;
        _s.Appearance.OffsetY = 2;
        UpdatePositionState();
        PersistNow();
    }

    private void OnMonitorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (MonitorBox.SelectedItem is not MonitorItem mon)
            return;

        _s.Monitor.DeviceName = mon.DeviceName;
        _s.Monitor.UsePrimary = mon.IsPrimary;
        _placement.ClearFreePosition(_s);
        _s.Appearance.HorizontalAlignment = "Right";
        _s.Appearance.OffsetX = 4;
        _s.Appearance.OffsetY = 2;
        UpdatePositionState();
        PersistNow();
    }

    private void UpdatePositionState()
    {
        var align = _s.Appearance.HorizontalAlignment?.ToLowerInvariant() switch
        {
            "left" => "左下角",
            "center" => "底部居中",
            _ => "右下角"
        };
        PositionStateText.Text = _s.Appearance.FreePosition
            ? "自由位置（已记住，可继续拖拽微调）"
            : "已贴边 · " + align;
    }

    // ==================================================================
    //  Appearance: theme presets
    // ==================================================================

    private void OnThemePreset(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string name)
            return;

        _s.Appearance.ThemeName = name;

        var palette = ThemeService.ResolveBarPalette(name);
        _s.Appearance.Background = palette.Background;
        _s.Appearance.Foreground = palette.Foreground;
        _s.Appearance.SeparatorColor = palette.SeparatorColor;

        LoadColorFields();
        UpdateThemeSelection();
        PersistNow();
    }

    private void UpdateThemeSelection()
    {
        var current = string.Equals(_s.Appearance.ThemeName, "Custom", StringComparison.OrdinalIgnoreCase)
            ? ""
            : _s.Appearance.ThemeName;

        foreach (var name in new[] { "System", "Light", "Dark", "HighContrast" })
        {
            if (FindName("ThemeBd_" + name) is Border bd)
                bd.BorderBrush = string.Equals(name, current, StringComparison.OrdinalIgnoreCase)
                    ? (Brush)FindResource("Fluent.Accent")
                    : Brushes.Transparent;
        }
    }

    // ==================================================================
    //  Appearance: colors
    // ==================================================================

    private void LoadColorFields()
    {
        BgColorBox.Text = _s.Appearance.Background;
        FgColorBox.Text = _s.Appearance.Foreground;
        UpdateSwatches();
    }

    private void UpdateSwatches()
    {
        BgSwatch.Background = ColorHelper.ToBrush(_s.Appearance.Background, Colors.White);
        FgSwatch.Background = ColorHelper.ToBrush(_s.Appearance.Foreground, Colors.Black);
    }

    private void OnColorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || sender is not TextBox box) return;

        // Only persist when the text is a complete, parseable color.
        var text = box.Text.Trim();
        if (!(text.StartsWith("#") && (text.Length == 7 || text.Length == 9)))
            return;

        if (box.Name == nameof(BgColorBox)) _s.Appearance.Background = text;
        else _s.Appearance.Foreground = text;

        _s.Appearance.ThemeName = "Custom";
        UpdateSwatches();
        UpdateThemeSelection();
        SchedulePersist();
    }

    private void OnPickBackground(object sender, RoutedEventArgs e) => PickColor(nameof(BgColorBox));
    private void OnPickForeground(object sender, RoutedEventArgs e) => PickColor(nameof(FgColorBox));

    private void PickColor(string which)
    {
        var current = which switch
        {
            nameof(BgColorBox) => ColorHelper.Parse(_s.Appearance.Background, Colors.White),
            _ => ColorHelper.Parse(_s.Appearance.Foreground, Colors.Black)
        };

        using var dlg = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B)
        };
        if (dlg.ShowDialog() != Forms.DialogResult.OK)
            return;

        var c = dlg.Color;
        var alpha = which == nameof(FgColorBox) ? (byte)255 : current.A;
        var hex = ColorHelper.ToHex(Color.FromArgb(alpha, c.R, c.G, c.B));

        if (which == nameof(BgColorBox)) BgColorBox.Text = hex;
        else FgColorBox.Text = hex;
    }

    // ==================================================================
    //  Appearance: font + sizes
    // ==================================================================

    private void LoadFontField()
    {
        var fonts = new List<string> { DefaultFontLabel };
        fonts.AddRange(Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
        FontFamilyBox.ItemsSource = fonts;

        var current = _s.Appearance.FontFamily;
        if (string.IsNullOrWhiteSpace(current) || current == DefaultFontValue)
            FontFamilyBox.SelectedIndex = 0;
        else
            FontFamilyBox.SelectedItem = fonts.FirstOrDefault(f => f == current) ?? fonts[0];
    }

    private void OnFontChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || FontFamilyBox.SelectedItem is not string name) return;
        _s.Appearance.FontFamily = FontFamilyBox.SelectedIndex == 0 ? DefaultFontValue : name;
        SchedulePersist();
    }

    private void OnSizeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;

        var a = _s.Appearance;
        a.FontSize = FontSizeSlider.Value;

        // Two stacked rows since brand v2: the bar has to grow with the time font, otherwise
        // the city name and the big time collide and the group is clipped by the window.
        var minHeight = AppearanceSettings.MinBarHeight(a.FontSize);
        if (Math.Abs(HeightSlider.Minimum - minHeight) > 0.5)
        {
            HeightSlider.Minimum = minHeight;
            if (HeightSlider.Value < minHeight)
                HeightSlider.Value = minHeight;   // re-enters once; the second pass is a no-op
        }

        a.BarHeight = Math.Max(HeightSlider.Value, minHeight);
        a.CornerRadius = RadiusSlider.Value;
        a.Opacity = Math.Round(OpacitySlider.Value, 2);

        FontSizeValue.Text = ((int)a.FontSize).ToString();
        HeightValue.Text = ((int)a.BarHeight).ToString();
        RadiusValue.Text = ((int)a.CornerRadius).ToString();
        OpacityValue.Text = a.Opacity.ToString("0.00");

        SchedulePersist();
    }

    private void LoadSizeFields()
    {
        var a = _s.Appearance;
        FontSizeSlider.Value = Math.Clamp(a.FontSize, 12, 32);
        HeightSlider.Minimum = AppearanceSettings.MinBarHeight(FontSizeSlider.Value);
        HeightSlider.Value = Math.Clamp(a.BarHeight, HeightSlider.Minimum, 96);
        RadiusSlider.Value = Math.Clamp(a.CornerRadius, 0, 20);
        OpacitySlider.Value = Math.Clamp(a.Opacity, 0.3, 1.0);
        FontSizeValue.Text = ((int)a.FontSize).ToString();
        HeightValue.Text = ((int)a.BarHeight).ToString();
        RadiusValue.Text = ((int)a.CornerRadius).ToString();
        OpacityValue.Text = a.Opacity.ToString("0.00");
    }

    // ==================================================================
    //  Behavior: autostart
    // ==================================================================

    private void OnAutoStartChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var enabled = AutoStartSwitch.IsChecked == true;
        try
        {
            _autostart.SetEnabled(enabled);
            _s.Behavior.AutoStart = enabled;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"设置开机自启失败：{ex.Message}", "WorldClockBar",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            AutoStartSwitch.IsChecked = _autostart.IsEnabled();
            return;
        }
        PersistNow();
    }

    // ==================================================================
    //  About / actions
    // ==================================================================

    private void OnOpenHomepage(object sender, RoutedEventArgs e)
        => OpenUrl("https://github.com/FeamCoco/FloatClock");

    private void OnOpenConfigFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_settingsService.SettingsPath}\""));
        }
        catch
        {
            // ignore
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // ignore
        }
    }

    private void OnResetDefaults(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "确定恢复默认配置吗？\n\n将重置城市、主题与位置（配置文件会被覆盖）。",
            "WorldClockBar", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
            return;

        var defaults = _settingsService.ResetToDefaults();
        _s = defaults;

        _loading = true;
        ReloadClockRows();
        LoadAllFields();
        UpdateThemeSelection();
        _loading = false;

        UpdatePositionState();
        SettingsChanged?.Invoke(_s);
        FlashStatus("已恢复默认配置");
    }

    // ==================================================================
    //  Persistence
    // ==================================================================

    private void LoadAllFields()
    {
        ShowSecondsSwitch.IsChecked = _s.Behavior.ShowSeconds;
        var use24 = !(_s.Behavior.TimeFormat ?? "").Contains("tt", StringComparison.Ordinal);
        HourFormatBox.SelectedIndex = use24 ? 0 : 1;

        var alignTag = _s.Appearance.HorizontalAlignment ?? "Right";
        AlignBox.SelectedItem = AlignBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(i => string.Equals(i.Tag as string, alignTag, StringComparison.OrdinalIgnoreCase))
            ?? AlignBox.Items[0];

        var monitors = _placement.ListMonitors()
            .Select(m => new MonitorItem(m.DeviceName, m.DisplayName, m.IsPrimary))
            .ToList();
        MonitorBox.ItemsSource = monitors;
        MonitorItem? selected = null;
        if (_s.Monitor.UsePrimary)
            selected = monitors.FirstOrDefault(m => m.IsPrimary);
        else if (!string.IsNullOrWhiteSpace(_s.Monitor.DeviceName))
            selected = monitors.FirstOrDefault(m =>
                string.Equals(m.DeviceName, _s.Monitor.DeviceName, StringComparison.OrdinalIgnoreCase));
        MonitorBox.SelectedItem = selected ?? monitors.FirstOrDefault();

        AutoStartSwitch.IsChecked = _s.Behavior.AutoStart;

        LoadFontField();
        LoadColorFields();
        LoadSizeFields();
        UpdatePositionState();
    }

    private void SchedulePersist()
    {
        if (_loading) return;
        _persistTimer.Stop();
        _persistTimer.Start();
    }

    private void PersistNow()
    {
        _persistTimer.Stop();
        if (_loading)
            return;

        SyncClocksFromRows();

        try
        {
            _settingsService.Save(_s);
        }
        catch (Exception ex)
        {
            FlashStatus("保存失败：" + ex.Message, warn: true);
            return;
        }

        SettingsChanged?.Invoke(_s);
        FlashStatus("已应用 · " + DateTime.Now.ToString("HH:mm:ss"));
    }

    private void FlashStatus(string message, bool warn = false)
    {
        StatusText.Text = (warn ? "⚠ " : "✓ ") + message;
        StatusPill.Visibility = Visibility.Visible;
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    // ==================================================================
    //  Row models
    // ==================================================================

    public sealed class ClockRow : INotifyPropertyChanged
    {
        private string _label;
        private string _timeZoneId;
        private string _timeZoneDisplay;

        public ClockRow(string label, string timeZoneId, string display)
        {
            _label = label;
            _timeZoneId = timeZoneId;
            _timeZoneDisplay = display;
        }

        public string Label
        {
            get => _label;
            set { _label = value; OnPropertyChanged(); }
        }

        public string TimeZoneId
        {
            get => _timeZoneId;
            set
            {
                _timeZoneId = value;
                OnPropertyChanged();
                try
                {
                    TimeZoneDisplay = TimeZoneInfo.FindSystemTimeZoneById(value).DisplayName;
                }
                catch
                {
                    TimeZoneDisplay = value;
                }
            }
        }

        public string TimeZoneDisplay
        {
            get => _timeZoneDisplay;
            set { _timeZoneDisplay = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed record TimeZoneItem(string Id, string DisplayName);
    public sealed record MonitorItem(string DeviceName, string DisplayName, bool IsPrimary);
}
