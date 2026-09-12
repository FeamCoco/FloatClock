using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WorldClockBar.Models;
using WorldClockBar.Services;
using Forms = System.Windows.Forms;

namespace WorldClockBar;

public partial class MainWindow : Window
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNomove = 0x0002;
    private const uint SwpNosize = 0x0001;
    private const uint SwpNoactivate = 0x0010;

    // ---------------------------------------------------------------- bar metrics
    // Brand spec §5.2. The group is now two stacked rows (name above, big time below),
    // so these are the single source of truth for both the builders and the width maths.
    private const double NameFontSize = 11;
    private const double SecondsFontSize = 12;
    private const double OffsetFontSize = 10;
    private const double GroupPaddingH = 8;      // gives the hover pill breathing room
    private const double GroupGap = 26;          // §5.1: replaces the 1px divider
    private const double RowGap = 2;
    private const double LocalDotSize = 6;
    private const double LocalDotGap = 6;
    private const double OffsetGap = 6;
    private const double SecondsGap = 4;

    /// <summary>
    /// Smallest bar height that keeps the two-row group intact at the current time font size.
    /// Derived, not fixed: a pre-v2 config can pair a tall font with the old 40px bar.
    /// </summary>
    private double MinBarHeight => AppearanceSettings.MinBarHeight(_fontSize);

    /// <summary>Widest realistic offset badge ("+13:45h"), used only for width measurement
    /// so a DST switch can never widen the badge past the already-computed bar width.</summary>
    private const string OffsetPlaceholder = "+13:45h";

    private readonly SettingsService _settingsService;
    private readonly TimeDisplayService _timeService = new();
    private readonly WindowPlacementService _placement = new();
    private readonly AutostartService _autostart = new();
    private readonly TrayIconService _tray;
    private readonly DispatcherTimer _timer;

    private AppSettings _settings;
    private SettingsWindow? _settingsWindow;
    private bool _isDragging;
    private Point _dragStart;
    private int _dragStartScreenX;
    private int _dragStartScreenY;
    private int _windowStartScreenX;
    private int _windowStartScreenY;
    private double _contentWidth = 180;

    // Bar visual state, refreshed by ApplyBarAppearance.
    private bool _acrylicActive;
    private bool _systemBackdrop;                 // DWMSBT acrylic (Win11 22H2+)
    private Brush _barBackgroundBrush = Brushes.White;
    private Brush _solidBackgroundBrush = Brushes.White;   // opaque fallback (no material)
    private Brush _materialBrush = Brushes.White;          // translucent tint over system acrylic
    private Brush _barOverlayBrush = Brushes.Transparent;
    private Brush _hoverBrush = Brushes.LightGray;
    private Brush _foregroundBrush = Brushes.Black;
    private Brush _secondaryBrush = Brushes.Gray;          // 城市名
    private Brush _tertiaryBrush = Brushes.Gray;           // 秒 / 时差
    private Brush _localDotBrush = Brushes.Teal;           // 本地城市标识点（品牌青）
    private Brush _warningBrush = Brushes.DarkOrange;      // 时区不可用
    private FontFamily _timeFont = new("Segoe UI");
    private double _fontSize = 19;
    private string _timeFormat = "HH:mm:ss";

    private sealed class CityItem
    {
        public Border Host = null!;
        public Ellipse? LocalDot;
        public TextBlock NameBlock = null!;
        public TextBlock OffsetBlock = null!;
        public TextBlock TimeBlock = null!;
        public Run MainRun = null!;
        public TextBlock? SecondsBlock;
        public Run? SecondsRun;
        public TextBlock? TipDate;
        public TextBlock? TipZone;
        public string TimeZoneId = "";
        public bool IsLocal;
        public bool IsValid = true;
    }

    private readonly List<CityItem> _cityItems = new();

    public MainWindow(SettingsService settingsService, AppSettings settings)
    {
        _settingsService = settingsService;
        _settings = settings;

        InitializeComponent();

        // Default: always on top (persisted in settings).
        if (!_settings.Behavior.AlwaysOnTop)
            _settings.Behavior.AlwaysOnTop = true;

        Topmost = true;
        Visibility = Visibility.Visible;
        ShowInTaskbar = false;

        _tray = new TrayIconService();
        _tray.OpenSettingsRequested += () => Dispatcher.Invoke(OpenSettings);
        _tray.ShowBarRequested += () => Dispatcher.Invoke(ShowAndSnap);
        _tray.ExitRequested += () => Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
        // Help user find the bar on first launch.
        Dispatcher.BeginInvoke(() =>
        {
            _tray.ShowBalloon("WorldClockBar 已启动",
                "时钟条在屏幕底部任务栏上方（默认右下角）。双击打开设置，托盘图标可右键「显示时钟条」。");
        }, DispatcherPriority.ApplicationIdle);

        // Hide from Alt-Tab via extended style after handle is ready.
        SourceInitialized += (_, _) =>
        {
            ApplyToolWindowStyle();
            ApplyBarMaterial();
            EnforceTopmost();
        };

        // Follow the OS light/dark switch when the bar theme is "System".
        ThemeService.SystemThemeChanged += OnSystemThemeChanged;

        ContentRendered += (_, _) =>
        {
            BuildBarItems();
            ShowAndSnap();
        };

        Deactivated += (_, _) =>
        {
            // Some apps steal Z-order; push ourselves back on top without activating.
            Dispatcher.BeginInvoke(EnforceTopmost, DispatcherPriority.ApplicationIdle);
        };

        Activated += (_, _) => EnforceTopmost();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) =>
        {
            UpdateTimes();
            EnforceTopmost();
        };
        _timer.Start();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyBarAppearance();
        BuildMonitorMenu();
        AutoStartMenuItem.IsChecked = _settings.Behavior.AutoStart;
        TopmostMenuItem.IsChecked = _settings.Behavior.AlwaysOnTop;
        BuildBarItems();
        ShowAndSnap();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _timer.Stop();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        ThemeService.SystemThemeChanged -= OnSystemThemeChanged;
        _settingsWindow?.Close();
        _tray.Dispose();
    }

    private void OnSystemThemeChanged()
    {
        Dispatcher.BeginInvoke(() =>
        {
            ApplyBarAppearance();
            // The tray icon follows the taskbar's light/dark setting, not the app theme —
            // it has to be re-picked here so a taskbar switch does not leave a stale glyph.
            _tray.RefreshIcon();
            if (_settingsWindow is { IsLoaded: true })
                _settingsWindow.RefreshSystemTheme();
        });
    }

    private void ShowAndSnap()
    {
        try
        {
            Visibility = Visibility.Visible;
            Show();
            if (Math.Abs(_settings.Appearance.OffsetX) > 5000)
                _settings.Appearance.OffsetX = 4;
            if (Math.Abs(_settings.Appearance.OffsetY) > 500)
                _settings.Appearance.OffsetY = 2;
            Reposition();
            EnforceTopmost();
            Activate();
        }
        catch
        {
            // ignore
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            BuildMonitorMenu();
            Reposition();
        });
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        // Keep always-on-top as the default expected behavior.
        if (!_settings.Behavior.AlwaysOnTop)
            _settings.Behavior.AlwaysOnTop = true;
        ApplyBarAppearance();
        BuildMonitorMenu();
        AutoStartMenuItem.IsChecked = _settings.Behavior.AutoStart;
        TopmostMenuItem.IsChecked = _settings.Behavior.AlwaysOnTop;
        BuildBarItems();
        EnforceTopmost();
    }

    // ==================================================================
    //  Appearance
    // ==================================================================

    private static bool IsLightForeground(Color c) =>
        (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0 > 0.55;

    private Color EffectiveBackgroundColor()
    {
        var a = _settings.Appearance;
        var palette = ThemeService.ResolveBarPalette(a.ThemeName);
        var isPreset = ThemeService.BarPalettes.Any(p =>
            string.Equals(p.Name, a.ThemeName, StringComparison.OrdinalIgnoreCase));
        return ColorHelper.Parse(
            isPreset ? palette.Background : a.Background,
            ColorHelper.Parse(palette.Background, Colors.White));
    }

    private Color EffectiveForegroundColor()
    {
        var a = _settings.Appearance;
        var palette = ThemeService.ResolveBarPalette(a.ThemeName);
        var isPreset = ThemeService.BarPalettes.Any(p =>
            string.Equals(p.Name, a.ThemeName, StringComparison.OrdinalIgnoreCase));
        return ColorHelper.Parse(
            isPreset ? palette.Foreground : a.Foreground,
            ColorHelper.Parse(palette.Foreground, Colors.Black));
    }

    private void ApplyBarAppearance()
    {
        var a = _settings.Appearance;
        var bg = EffectiveBackgroundColor();
        var fg = EffectiveForegroundColor();
        var darkSurface = IsLightForeground(fg);

        _barBackgroundBrush = Frozen(bg);
        _solidBackgroundBrush = Frozen(Color.FromRgb(bg.R, bg.G, bg.B));
        // Over the system acrylic the brush only tints — the material adds its own
        // smoke layer, so clamp heavy alphas or the bar goes near-solid (mockup ≈ 74%).
        _materialBrush = Frozen(Color.FromArgb(Math.Min(bg.A, (byte)0xB4), bg.R, bg.G, bg.B));
        _barOverlayBrush = Frozen(darkSurface
            ? Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF));
        // §5.2 hover: 深色 6% 白 / 浅色 5% 黑.
        _hoverBrush = Frozen(darkSurface
            ? Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(0x0D, 0x00, 0x00, 0x00));
        _foregroundBrush = Frozen(fg);
        // Run/TextBlock hierarchy on the bar is expressed by colour, not opacity, so the
        // three levels are pre-mixed against the bar foreground: 时分 100% / 城市名 70% / 秒 44%.
        _secondaryBrush = Frozen(Color.FromArgb(0xB4, fg.R, fg.G, fg.B));
        _tertiaryBrush = Frozen(Color.FromArgb(0x70, fg.R, fg.G, fg.B));
        // Brand spec §2.3/§2.4: the local-city dot is locked to Meridian teal — it must never
        // follow the system accent, or the icon and the UI would contradict each other.
        _localDotBrush = Frozen(darkSurface
            ? Color.FromRgb(0x3A, 0xAF, 0xAA)   // §2.4 dark accent
            : Color.FromRgb(0x0F, 0x76, 0x6E)); // §2.4 light accent / §2.3 Local
        _warningBrush = Frozen((Color)ColorConverter.ConvertFromString("#C77700"));
        _timeFont = new FontFamily(string.IsNullOrWhiteSpace(a.FontFamily) ? "Segoe UI" : a.FontFamily);
        _fontSize = Math.Clamp(a.FontSize, 9, 40);
        _timeFormat = ResolveFormat(_settings.Behavior);

        UpdateRootBackground();
        RootBorder.CornerRadius = new CornerRadius(Math.Max(0, a.CornerRadius));
        RootBorder.Opacity = a.Opacity;
        Height = Math.Max(MinBarHeight, a.BarHeight);
        // Restyle already-built items.
        foreach (var item in _cityItems)
            StyleCityItem(item, item.IsValid);

        // The acrylic tint lives in DWM, not in WPF brushes — refresh it on every
        // palette change so theme switches take effect immediately.
        if (new WindowInteropHelper(this).Handle != IntPtr.Zero)
            ApplyBarMaterial();
    }

    private void UpdateRootBackground()
    {
        RootBorder.Background = _systemBackdrop ? _materialBrush
            : _acrylicActive ? _barOverlayBrush
            : _solidBackgroundBrush;
    }

    /// <summary>Enable (or refresh) the bar material: system acrylic on Win11 22H2+,
    /// legacy blur-behind accent on older systems, opaque tint when neither works.</summary>
    private void ApplyBarMaterial()
    {
        var tint = EffectiveBackgroundColor();
        var dark = IsLightForeground(EffectiveForegroundColor());

        _systemBackdrop = ThemeService.TrySetSystemAcrylic(this, dark);
        if (_systemBackdrop)
        {
            _acrylicActive = true;
        }
        else
        {
            _acrylicActive = ThemeService.TrySetAcrylic(this, tint);
            if (!_acrylicActive)
                ThemeService.ClearSystemBackdrop(this);
        }
        UpdateRootBackground();
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static string ResolveFormat(BehaviorSettings behavior)
    {
        if (!string.IsNullOrWhiteSpace(behavior.TimeFormat))
            return behavior.TimeFormat;
        return behavior.ShowSeconds ? "HH:mm:ss" : "HH:mm";
    }

    // ==================================================================
    //  Bar content: build once, then only text updates per tick
    // ==================================================================

    /// <summary>
    /// Bar content: build once, then only the text updates per tick. §5.1 replaced the flat
    /// "name + time on one row, 1px divider between cities" strip with one group per city —
    /// name row on top, big time row underneath, whitespace instead of dividers.
    /// </summary>
    private void BuildBarItems()
    {
        ClocksPanel.Items.Clear();
        _cityItems.Clear();

        var clocks = _settings.Clocks;
        var localZoneId = SafeLocalZoneId();

        for (var i = 0; i < clocks.Count; i++)
        {
            var clock = clocks[i];
            var label = string.IsNullOrWhiteSpace(clock.Label) ? clock.TimeZoneId : clock.Label.Trim();
            var isLocal = localZoneId.Length > 0
                          && string.Equals(clock.TimeZoneId, localZoneId, StringComparison.OrdinalIgnoreCase);

            var item = new CityItem
            {
                TimeZoneId = clock.TimeZoneId,
                IsLocal = isLocal,
                NameBlock = new TextBlock
                {
                    Text = label,
                    FontFamily = _timeFont,
                    FontSize = NameFontSize,
                    VerticalAlignment = VerticalAlignment.Center
                },
                OffsetBlock = new TextBlock
                {
                    FontFamily = _timeFont,
                    FontSize = OffsetFontSize,
                    Margin = new Thickness(OffsetGap, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            // ---- row 1: [品牌青圆点] 城市名 · 时差 ----
            var nameRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (isLocal)
            {
                item.LocalDot = new Ellipse
                {
                    Width = LocalDotSize,
                    Height = LocalDotSize,
                    Margin = new Thickness(0, 0, LocalDotGap, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                nameRow.Children.Add(item.LocalDot);
            }
            nameRow.Children.Add(item.NameBlock);
            nameRow.Children.Add(item.OffsetBlock);

            // ---- row 2: 大号时分 + 小号秒（两条 TextBlock 底对齐，§5.2）----
            var timeRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, RowGap, 0, 0)
            };

            item.TimeBlock = new TextBlock
            {
                FontFamily = _timeFont,
                FontSize = _fontSize,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            // Tabular digits: "1" occupies the same advance width as "8", so seconds
            // tick without the digits wobbling.
            item.TimeBlock.Typography.NumeralAlignment = FontNumeralAlignment.Tabular;
            item.MainRun = new Run("--:--");
            item.TimeBlock.Inlines.Add(item.MainRun);
            timeRow.Children.Add(item.TimeBlock);

            if (_timeFormat.Contains(":ss", StringComparison.Ordinal))
            {
                item.SecondsBlock = new TextBlock
                {
                    FontFamily = _timeFont,
                    FontSize = SecondsFontSize,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    // Sit the seconds on the big time's baseline instead of its descender
                    // (≈0.2em) — same trick the city name used to use.
                    Margin = new Thickness(SecondsGap, 0, 0, Math.Round(_fontSize * 0.2))
                };
                item.SecondsBlock.Typography.NumeralAlignment = FontNumeralAlignment.Tabular;
                item.SecondsRun = new Run(":00");
                item.SecondsBlock.Inlines.Add(item.SecondsRun);
                timeRow.Children.Add(item.SecondsBlock);
            }

            var group = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };
            group.Children.Add(nameRow);
            group.Children.Add(timeRow);

            var tip = new StackPanel { Orientation = Orientation.Vertical };
            item.TipDate = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold };
            item.TipZone = new TextBlock { FontSize = 12, Margin = new Thickness(0, 2, 0, 0) };
            item.TipDate.SetResourceReference(TextBlock.ForegroundProperty, "Fluent.TextPrimary");
            item.TipZone.SetResourceReference(TextBlock.ForegroundProperty, "Fluent.TextSecondary");
            tip.Children.Add(item.TipDate);
            tip.Children.Add(item.TipZone);

            item.Host = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(GroupPaddingH, 4, GroupPaddingH, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                Child = group,
                ToolTip = new ToolTip { Content = tip, Placement = System.Windows.Controls.Primitives.PlacementMode.Top },
                // §5.1: 组间靠留白划分。StackPanel 不合并相邻 Margin，所以只在第二个
                // 之后的每个组上加一个完整间距，视觉间隔正好等于 GroupGap。
                Margin = new Thickness(i == 0 ? 0 : GroupGap, 0, 0, 0)
            };
            item.Host.MouseEnter += (_, _) => item.Host.Background = _hoverBrush;
            item.Host.MouseLeave += (_, _) => item.Host.Background = Brushes.Transparent;

            ClocksPanel.Items.Add(item.Host);
            _cityItems.Add(item);
            StyleCityItem(item, true);
        }

        ComputeBarWidth();
        UpdateTimes();
    }

    private static string SafeLocalZoneId()
    {
        try
        {
            return TimeZoneInfo.Local.Id;
        }
        catch
        {
            return "";
        }
    }

    /// <summary>§5.1: 城市名行右侧的时差标识（本地 / +8h / −5:30h）。U+2212 minus matches the design.</summary>
    private static string FormatOffset(TimeSpan delta)
    {
        if (delta == TimeSpan.Zero)
            return "本地";

        var sign = delta < TimeSpan.Zero ? "\u2212" : "+";
        delta = delta.Duration();
        return delta.Minutes == 0
            ? $"{sign}{delta.Hours}h"
            : $"{sign}{delta.Hours}:{delta.Minutes:00}h";
    }

    private void StyleCityItem(CityItem item, bool isValid)
    {
        item.IsValid = isValid;

        if (!isValid)
        {
            item.NameBlock.Foreground = _warningBrush;
            item.OffsetBlock.Foreground = _warningBrush;
            item.MainRun.Foreground = _warningBrush;
            if (item.SecondsRun is not null) item.SecondsRun.Foreground = _warningBrush;
            if (item.LocalDot is not null) item.LocalDot.Fill = _warningBrush;
            return;
        }

        item.NameBlock.Foreground = _secondaryBrush;
        item.OffsetBlock.Foreground = _tertiaryBrush;
        item.MainRun.Foreground = _foregroundBrush;
        if (item.SecondsRun is not null)
            item.SecondsRun.Foreground = _tertiaryBrush;
        if (item.LocalDot is not null)
            item.LocalDot.Fill = _localDotBrush;
    }

    private void UpdateTimes()
    {
        var lines = _timeService.BuildLines(_settings.Clocks, _settings.Behavior);
        var utcNow = DateTime.UtcNow;
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(utcNow);

        for (var i = 0; i < lines.Count && i < _cityItems.Count; i++)
        {
            var line = lines[i];
            var item = _cityItems[i];

            if (!line.IsValid)
            {
                item.MainRun.Text = "--:--";
                if (item.SecondsRun is not null) item.SecondsRun.Text = "";
                item.OffsetBlock.Text = "";
                StyleCityItem(item, false);
            }
            else
            {
                SplitTime(line.TimeText, item);
                StyleCityItem(item, true);
            }

            // Tooltip + offset badge share one zone lookup. The badge is refreshed here
            // rather than only at build time so a DST switch is picked up within a second.
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId);
                var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone);
                item.TipDate!.Text = local.ToString("yyyy/M/d dddd", CultureInfo.CurrentCulture);
                var offset = zone.GetUtcOffset(local); // includes DST
                var sign = offset >= TimeSpan.Zero ? "+" : "-";
                item.TipZone!.Text = $"{zone.Id} · UTC{sign}{Math.Abs(offset.Hours)}:{Math.Abs(offset.Minutes):00}";
                if (line.IsValid)
                    item.OffsetBlock.Text = FormatOffset(offset - localOffset);
            }
            catch
            {
                item.TipDate!.Text = "";
                item.TipZone!.Text = "时区不可用";
                item.OffsetBlock.Text = "";
            }
        }
    }

    /// <summary>Split "HH:mm:ss …" so the seconds land in their own small TextBlock (§5.2).</summary>
    private void SplitTime(string text, CityItem item)
    {
        var idx = _timeFormat.IndexOf(":ss", StringComparison.Ordinal);
        if (idx >= 0 && text.Length >= idx + 3 && string.IsNullOrEmpty(_settings.Behavior.DateFormat))
        {
            item.MainRun.Text = text[..idx];
            item.SecondsRun!.Text = text[idx..];
        }
        else
        {
            item.MainRun.Text = text;
            if (item.SecondsRun is not null) item.SecondsRun.Text = "";
        }
    }

    /// <summary>
    /// Constant-width measurement: measure with every digit replaced by "8" so the
    /// bar never resizes as seconds tick. Text updates alone don't re-measure.
    /// Measures the wider of the two group rows per city, then adds the group padding
    /// and the inter-group gaps that replaced the old divider lines.
    /// </summary>
    private void ComputeBarWidth()
    {
        var a = _settings.Appearance;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var nameTypeface = new Typeface(_timeFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var timeTypeface = new Typeface(_timeFont, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        // The bar splits "HH:mm" and ":ss" into two TextBlocks, so the placeholder has to be
        // split the same way. Measuring the whole "88:88:88" and *then* adding the seconds
        // block would reserve the seconds twice and leave a visible gap inside every group.
        var format = ResolveFormat(_settings.Behavior);
        var placeholder = PlaceholderFor(format);
        var ssIndex = format.IndexOf(":ss", StringComparison.Ordinal);
        var hasDate = !string.IsNullOrWhiteSpace(_settings.Behavior.DateFormat);
        // SplitTime only splits when there is no date prefix, so the widths must agree.
        var splits = ssIndex >= 0 && !hasDate;

        var mainPlaceholder = splits
            ? placeholder[..ssIndex]
            : hasDate ? PlaceholderFor(_settings.Behavior.DateFormat!) + " " + placeholder
            : placeholder;

        var timeRowWidth = Measure(timeTypeface, mainPlaceholder, _fontSize, dpi);
        if (splits)
            timeRowWidth += SecondsGap + Measure(nameTypeface, ":00", SecondsFontSize, dpi);

        var localZoneId = SafeLocalZoneId();

        double textWidth = 0;
        foreach (var clock in _settings.Clocks)
        {
            var label = string.IsNullOrWhiteSpace(clock.Label) ? clock.TimeZoneId : clock.Label.Trim();
            var isLocal = localZoneId.Length > 0
                          && string.Equals(clock.TimeZoneId, localZoneId, StringComparison.OrdinalIgnoreCase);

            // Row 1 is normally the narrower row, but a long city name can beat the time —
            // measure both and take the wider one.
            var nameRowWidth =
                (isLocal ? LocalDotSize + LocalDotGap : 0)
                + Measure(nameTypeface, label, NameFontSize, dpi)
                + OffsetGap
                + Measure(nameTypeface, OffsetPlaceholder, OffsetFontSize, dpi);

            textWidth += Math.Max(nameRowWidth, timeRowWidth) + GroupPaddingH * 2;
        }

        // Safety: if font measurement fails (composite family quirk), fall back to a
        // generous per-city estimate so the bar never clips.
        if (textWidth < _settings.Clocks.Count * 40)
            textWidth = _settings.Clocks.Count * 120;

        var chrome = RootBorder.Padding.Left + RootBorder.Padding.Right
                     + RootBorder.BorderThickness.Left + RootBorder.BorderThickness.Right
                     + Math.Max(0, _settings.Clocks.Count - 1) * GroupGap
                     + 2; // safety

        _contentWidth = Math.Max(64, Math.Ceiling(textWidth + chrome));
        Width = _contentWidth;
        Height = Math.Max(MinBarHeight, a.BarHeight);
    }

    private static double Measure(Typeface typeface, string text, double size, double pixelsPerDip)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            size,
            Brushes.Black,
            pixelsPerDip);
        return ft.WidthIncludingTrailingWhitespace;
    }

    private static string PlaceholderFor(string format)
    {
        var chars = format.Select(c => c switch
        {
            'H' or 'h' or 'm' or 's' => '8',
            't' => 'A',
            _ => c
        }).ToArray();
        return new string(chars);
    }

    private void Reposition()
    {
        Width = _contentWidth;
        Height = Math.Max(MinBarHeight, _settings.Appearance.BarHeight);
        _placement.PlaceBar(this, _settings, _contentWidth);
    }

    // ==================================================================
    //  Monitor menu
    // ==================================================================

    private void BuildMonitorMenu()
    {
        var menu = RootBorder.ContextMenu;
        if (menu is null) return;

        // Rebuild monitor submenu items.
        var monitorItem = menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "MonitorMenu");
        if (monitorItem is null) return;

        monitorItem.Items.Clear();
        var monitors = _placement.ListMonitors();
        foreach (var mon in monitors)
        {
            var item = new MenuItem
            {
                Header = mon.DisplayName,
                IsCheckable = true,
                IsChecked = IsSelectedMonitor(mon.DeviceName, mon.IsPrimary),
                Tag = mon.DeviceName
            };
            item.Click += OnSelectMonitor;
            monitorItem.Items.Add(item);
        }
    }

    private bool IsSelectedMonitor(string deviceName, bool isPrimary)
    {
        if (_settings.Monitor.UsePrimary)
            return isPrimary;
        return string.Equals(_settings.Monitor.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase);
    }

    private void OnSelectMonitor(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string deviceName)
            return;

        var primary = Forms.Screen.PrimaryScreen?.DeviceName;
        _settings.Monitor.DeviceName = deviceName;
        _settings.Monitor.UsePrimary = string.Equals(deviceName, primary, StringComparison.OrdinalIgnoreCase);
        // Dock to bottom-right of the new monitor with a small margin.
        _placement.ClearFreePosition(_settings);
        _settings.Appearance.HorizontalAlignment = "Right";
        _settings.Appearance.OffsetX = 4;
        _settings.Appearance.OffsetY = 2;
        try { _settingsService.Save(_settings); } catch { /* ignore */ }
        BuildMonitorMenu();
        Reposition();
    }

    // ==================================================================
    //  Settings window wiring
    // ==================================================================

    private void OnOpenSettings(object sender, RoutedEventArgs e) => OpenSettings();

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSettings();

    private void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settingsService, _settings, _autostart, _placement);
        _settingsWindow.SettingsChanged += updated => ApplySettings(updated);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnToggleAutoStart(object sender, RoutedEventArgs e)
    {
        var enabled = AutoStartMenuItem.IsChecked == true;
        try
        {
            _autostart.SetEnabled(enabled);
            _settings.Behavior.AutoStart = enabled;
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"设置开机自启失败：{ex.Message}", "WorldClockBar",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            AutoStartMenuItem.IsChecked = _autostart.IsEnabled();
        }
    }

    private void OnToggleTopmost(object sender, RoutedEventArgs e)
    {
        // User asked for persistent always-on-top; keep it forced on.
        _settings.Behavior.AlwaysOnTop = true;
        TopmostMenuItem.IsChecked = true;
        EnforceTopmost();
        try { _settingsService.Save(_settings); } catch { /* ignore */ }
    }

    /// <summary>
    /// Re-assert HWND_TOPMOST so the bar stays above normal windows even after
    /// focus changes or other topmost windows briefly steal Z-order.
    /// </summary>
    private void EnforceTopmost()
    {
        if (!_settings.Behavior.AlwaysOnTop)
            return;

        try
        {
            if (!Topmost)
                Topmost = true;

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0,
                SwpNomove | SwpNosize | SwpNoactivate);
        }
        catch
        {
            // ignore
        }
    }

    private void OnReposition(object sender, RoutedEventArgs e)
    {
        // Snap back to bottom-right (recover / clear free position).
        _placement.ClearFreePosition(_settings);
        _settings.Appearance.HorizontalAlignment = "Right";
        _settings.Appearance.OffsetX = 4;
        _settings.Appearance.OffsetY = 2;
        ShowAndSnap();
        try { _settingsService.Save(_settings); } catch { /* ignore */ }
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    // ==================================================================
    //  Dragging (acrylic is disabled mid-drag to avoid smear)
    // ==================================================================

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1)
            return;

        _isDragging = true;
        _dragStart = e.GetPosition(this);

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var rect))
        {
            _windowStartScreenX = rect.Left;
            _windowStartScreenY = rect.Top;
        }
        else
        {
            var tl = PointToScreen(new Point(0, 0));
            _windowStartScreenX = (int)tl.X;
            _windowStartScreenY = (int)tl.Y;
        }

        var mouseScreen = PointToScreen(_dragStart);
        _dragStartScreenX = (int)mouseScreen.X;
        _dragStartScreenY = (int)mouseScreen.Y;

        // Legacy accent blur smears while dragging — drop to opaque solid for the drag.
        // The Win11 system backdrop tracks the window live, so it can stay on.
        if (!_systemBackdrop)
        {
            ThemeService.ClearAcrylic(this);
            RootBorder.Background = _solidBackgroundBrush;
        }

        CaptureMouse();
        MouseMove += OnDragMove;
        MouseLeftButtonUp += OnDragEnd;
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
            return;

        var mouseScreen = PointToScreen(e.GetPosition(this));
        var dx = (int)mouseScreen.X - _dragStartScreenX;
        var dy = (int)mouseScreen.Y - _dragStartScreenY;

        // Free drag inside working area (excludes taskbar). Live edge snap while dragging.
        _placement.DragTo(
            this,
            _settings,
            _windowStartScreenX + dx,
            _windowStartScreenY + dy,
            _contentWidth,
            snapEdges: true);
    }

    private void OnDragEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        ReleaseMouseCapture();
        MouseMove -= OnDragMove;
        MouseLeftButtonUp -= OnDragEnd;

        // Final snap + persist free position inside working area.
        var hwnd = new WindowInteropHelper(this).Handle;
        int left = _windowStartScreenX;
        int top = _windowStartScreenY;
        if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var rect))
        {
            left = rect.Left;
            top = rect.Top;
        }

        var result = _placement.DragTo(this, _settings, left, top, _contentWidth, snapEdges: true);
        _placement.SaveFreePosition(_settings, result.Screen, result.X, result.Y);
        try { _settingsService.Save(_settings); } catch { /* ignore */ }

        ApplyBarMaterial();
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RectPx lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectPx
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private void ApplyToolWindowStyle()
    {
        var helper = new WindowInteropHelper(this);
        var hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero)
            return;

        // ToolWindow: hide from Alt-Tab / taskbar switcher. Avoid NOACTIVATE so context menu works.
        const int gwlExStyle = -20;
        const int wsExToolwindow = 0x00000080;

        var ex = GetWindowLong(hwnd, gwlExStyle);
        ex |= wsExToolwindow;
        SetWindowLong(hwnd, gwlExStyle, ex);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
