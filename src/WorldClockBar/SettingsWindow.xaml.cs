using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WorldClockBar.Models;
using WorldClockBar.Services;
using Forms = System.Windows.Forms;

namespace WorldClockBar;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly AutostartService _autostart;
    private readonly WindowPlacementService _placement;
    private readonly AppSettings _working;

    public event EventHandler<AppSettings>? SettingsSaved;

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
        _working = Clone(current);

        TimeZones = TimeDisplayService.GetSystemTimeZones()
            .Select(z => new TimeZoneItem(z.Id, z.DisplayName))
            .ToList();

        DataContext = this;
        InitializeComponent();

        foreach (var c in _working.Clocks)
            ClockRows.Add(new ClockRow(c.Label, c.TimeZoneId, ResolveDisplay(c.TimeZoneId)));

        ClocksGrid.ItemsSource = ClockRows;

        // Fonts
        foreach (var ff in Fonts.SystemFontFamilies.OrderBy(f => f.Source))
            FontFamilyBox.Items.Add(ff.Source);

        LoadAppearanceFields();
        LoadMonitors();
        LoadBehavior();

        FontSizeSlider.ValueChanged += (_, _) => FontSizeLabel.Text = ((int)FontSizeSlider.Value).ToString();
        HeightSlider.ValueChanged += (_, _) => HeightLabel.Text = ((int)HeightSlider.Value).ToString();
        OpacitySlider.ValueChanged += (_, _) => OpacityLabel.Text = OpacitySlider.Value.ToString("0.00");
        RadiusSlider.ValueChanged += (_, _) => RadiusLabel.Text = ((int)RadiusSlider.Value).ToString();
        MarginSlider.ValueChanged += (_, _) => MarginLabel.Text = ((int)MarginSlider.Value).ToString();

        ConfigPathText.Text = "配置文件：" + _settingsService.SettingsPath;
    }

    private void LoadAppearanceFields()
    {
        var a = _working.Appearance;
        BgColorBox.Text = a.Background;
        FgColorBox.Text = a.Foreground;
        SepColorBox.Text = a.SeparatorColor;
        FontFamilyBox.Text = a.FontFamily;
        FontSizeSlider.Value = a.FontSize;
        FontSizeLabel.Text = ((int)a.FontSize).ToString();
        HeightSlider.Value = a.BarHeight;
        HeightLabel.Text = ((int)a.BarHeight).ToString();
        OpacitySlider.Value = a.Opacity;
        OpacityLabel.Text = a.Opacity.ToString("0.00");
        RadiusSlider.Value = a.CornerRadius;
        RadiusLabel.Text = ((int)a.CornerRadius).ToString();

        foreach (ComboBoxItem item in AlignBox.Items)
        {
            if (string.Equals(item.Tag as string, a.HorizontalAlignment, StringComparison.OrdinalIgnoreCase))
            {
                AlignBox.SelectedItem = item;
                break;
            }
        }
        if (AlignBox.SelectedItem is null && AlignBox.Items.Count > 0)
            AlignBox.SelectedIndex = 0;

        MarginSlider.Value = a.OffsetX;
        MarginLabel.Text = ((int)a.OffsetX).ToString();
    }

    private void LoadMonitors()
    {
        var list = _placement.ListMonitors()
            .Select(m => new MonitorItem(m.DeviceName, m.DisplayName, m.IsPrimary))
            .ToList();
        MonitorBox.ItemsSource = list;

        MonitorItem? selected = null;
        if (_working.Monitor.UsePrimary)
            selected = list.FirstOrDefault(m => m.IsPrimary);
        else if (!string.IsNullOrWhiteSpace(_working.Monitor.DeviceName))
            selected = list.FirstOrDefault(m =>
                string.Equals(m.DeviceName, _working.Monitor.DeviceName, StringComparison.OrdinalIgnoreCase));

        MonitorBox.SelectedItem = selected ?? list.FirstOrDefault();
    }

    private void LoadBehavior()
    {
        ShowSecondsBox.IsChecked = _working.Behavior.ShowSeconds;
        AutoStartBox.IsChecked = _working.Behavior.AutoStart || _autostart.IsEnabled();
    }

    private void CollectIntoWorking()
    {
        _working.Clocks = ClockRows
            .Select(r => new ClockEntry
            {
                Label = string.IsNullOrWhiteSpace(r.Label) ? "时钟" : r.Label.Trim(),
                TimeZoneId = string.IsNullOrWhiteSpace(r.TimeZoneId) ? "UTC" : r.TimeZoneId
            })
            .ToList();

        var a = _working.Appearance;
        a.Background = BgColorBox.Text.Trim();
        a.Foreground = FgColorBox.Text.Trim();
        a.SeparatorColor = SepColorBox.Text.Trim();
        a.FontFamily = string.IsNullOrWhiteSpace(FontFamilyBox.Text) ? "Segoe UI" : FontFamilyBox.Text.Trim();
        a.FontSize = FontSizeSlider.Value;
        a.BarHeight = HeightSlider.Value;
        a.Opacity = OpacitySlider.Value;
        a.CornerRadius = RadiusSlider.Value;
        var newAlign = (AlignBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Right";
        var newMargin = MarginSlider.Value;
        // Changing dock alignment/margin exits free-drag mode and re-docks.
        if (!string.Equals(a.HorizontalAlignment, newAlign, StringComparison.OrdinalIgnoreCase)
            || Math.Abs(a.OffsetX - newMargin) > 0.01)
        {
            a.FreePosition = false;
        }
        a.HorizontalAlignment = newAlign;
        a.OffsetX = newMargin;

        if (MonitorBox.SelectedItem is MonitorItem mon)
        {
            _working.Monitor.DeviceName = mon.DeviceName;
            _working.Monitor.UsePrimary = mon.IsPrimary;
        }

        _working.Behavior.ShowSeconds = ShowSecondsBox.IsChecked == true;
        _working.Behavior.TimeFormat = _working.Behavior.ShowSeconds ? "HH:mm:ss" : "HH:mm";
        _working.Behavior.AutoStart = AutoStartBox.IsChecked == true;
        _working.Behavior.AlwaysOnTop = true; // always keep bar on top
    }

    private bool TrySave(bool closeAfter)
    {
        CollectIntoWorking();

        if (_working.Clocks.Count == 0)
        {
            MessageBox.Show("请至少保留一个城市。", "WorldClockBar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        try
        {
            _settingsService.Save(_working);
            _autostart.SetEnabled(_working.Behavior.AutoStart);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "WorldClockBar", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        SettingsSaved?.Invoke(this, Clone(_working));
        StatusText.Text = "已保存 " + DateTime.Now.ToString("HH:mm:ss");
        if (closeAfter)
            Close();
        return true;
    }

    private void OnApply(object sender, RoutedEventArgs e) => TrySave(closeAfter: false);
    private void OnOk(object sender, RoutedEventArgs e) => TrySave(closeAfter: true);
    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnAddClock(object sender, RoutedEventArgs e)
    {
        var tz = TimeZones.FirstOrDefault(t => t.Id == "China Standard Time")
                 ?? TimeZones.FirstOrDefault();
        if (tz is null) return;
        ClockRows.Add(new ClockRow("新城市", tz.Id, tz.DisplayName));
    }

    private void OnRemoveClock(object sender, RoutedEventArgs e)
    {
        if (ClocksGrid.SelectedItem is ClockRow row)
            ClockRows.Remove(row);
    }

    private void OnMoveUp(object sender, RoutedEventArgs e)
    {
        if (ClocksGrid.SelectedItem is not ClockRow row) return;
        var i = ClockRows.IndexOf(row);
        if (i <= 0) return;
        ClockRows.Move(i, i - 1);
        ClocksGrid.SelectedItem = row;
    }

    private void OnMoveDown(object sender, RoutedEventArgs e)
    {
        if (ClocksGrid.SelectedItem is not ClockRow row) return;
        var i = ClockRows.IndexOf(row);
        if (i < 0 || i >= ClockRows.Count - 1) return;
        ClockRows.Move(i, i + 1);
        ClocksGrid.SelectedItem = row;
    }

    private void OnPickBackground(object sender, RoutedEventArgs e) => PickColorInto(BgColorBox);
    private void OnPickForeground(object sender, RoutedEventArgs e) => PickColorInto(FgColorBox);
    private void OnPickSeparator(object sender, RoutedEventArgs e) => PickColorInto(SepColorBox);

    private void PickColorInto(TextBox box)
    {
        var current = ColorHelper.Parse(box.Text, Colors.Gray);
        using var dlg = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B)
        };
        if (dlg.ShowDialog() != Forms.DialogResult.OK)
            return;

        var c = dlg.Color;
        // Preserve alpha from existing text if present; default opaque for text colors, keep alpha for bg.
        byte alpha = current.A;
        if (ReferenceEquals(box, FgColorBox))
            alpha = 255;
        box.Text = ColorHelper.ToHex(Color.FromArgb(alpha, c.R, c.G, c.B));
    }

    private void OnThemeDark(object sender, RoutedEventArgs e)
        => ApplyTheme("#CC1E1E1E", "#FFFFFFFF", "#88FFFFFF", 0.92);

    private void OnThemeLight(object sender, RoutedEventArgs e)
        => ApplyTheme("#F2FFFFFF", "#FF111111", "#66111111", 1.0);

    private void OnThemeHighContrast(object sender, RoutedEventArgs e)
        => ApplyTheme("#FF000000", "#FFFFFF00", "#FFFF00FF", 1.0);

    private void OnThemeBlue(object sender, RoutedEventArgs e)
        => ApplyTheme("#CC0B1F33", "#FFE8F3FF", "#88A0C8E8", 0.92);

    private void ApplyTheme(string bg, string fg, string sep, double opacity)
    {
        BgColorBox.Text = bg;
        FgColorBox.Text = fg;
        SepColorBox.Text = sep;
        OpacitySlider.Value = opacity;
    }

    private string ResolveDisplay(string id)
        => TimeZones.FirstOrDefault(t => t.Id == id)?.DisplayName ?? id;

    private static AppSettings Clone(AppSettings s) => new()
    {
        Clocks = s.Clocks.Select(c => new ClockEntry { Label = c.Label, TimeZoneId = c.TimeZoneId }).ToList(),
        Appearance = new AppearanceSettings
        {
            Background = s.Appearance.Background,
            Foreground = s.Appearance.Foreground,
            SeparatorColor = s.Appearance.SeparatorColor,
            FontFamily = s.Appearance.FontFamily,
            FontSize = s.Appearance.FontSize,
            Opacity = s.Appearance.Opacity,
            BarHeight = s.Appearance.BarHeight,
            CornerRadius = s.Appearance.CornerRadius,
            HorizontalAlignment = s.Appearance.HorizontalAlignment,
            OffsetX = s.Appearance.OffsetX,
            OffsetY = s.Appearance.OffsetY,
            FreePosition = s.Appearance.FreePosition,
            PosX = s.Appearance.PosX,
            PosY = s.Appearance.PosY,
            EdgeSnapDistance = s.Appearance.EdgeSnapDistance
        },
        Monitor = new MonitorSettings
        {
            DeviceName = s.Monitor.DeviceName,
            UsePrimary = s.Monitor.UsePrimary
        },
        Behavior = new BehaviorSettings
        {
            AutoStart = s.Behavior.AutoStart,
            ShowSeconds = s.Behavior.ShowSeconds,
            AlwaysOnTop = s.Behavior.AlwaysOnTop,
            TimeFormat = s.Behavior.TimeFormat,
            DateFormat = s.Behavior.DateFormat
        }
    };

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
                // Update display name when id changes via combo.
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
