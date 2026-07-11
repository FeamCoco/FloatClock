using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Globalization;
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
                "白色时钟条在屏幕底部任务栏上方（默认：英国时间）。托盘图标可右键「显示时钟条」。");
        }, DispatcherPriority.ApplicationIdle);

        // Hide from Alt-Tab via extended style after handle is ready.
        SourceInitialized += (_, _) =>
        {
            ApplyToolWindowStyle();
            EnforceTopmost();
        };

        ContentRendered += (_, _) =>
        {
            // After first render, force measure + snap so the bar is visible.
            RefreshClocks(reposition: true);
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
            RefreshClocks(reposition: false);
            EnforceTopmost();
        };
        _timer.Start();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyAppearance();
        BuildMonitorMenu();
        AutoStartMenuItem.IsChecked = _settings.Behavior.AutoStart;
        TopmostMenuItem.IsChecked = _settings.Behavior.AlwaysOnTop;
        RefreshClocks(reposition: true);
        ShowAndSnap();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _timer.Stop();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _settingsWindow?.Close();
        _tray.Dispose();
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
        ApplyAppearance();
        BuildMonitorMenu();
        AutoStartMenuItem.IsChecked = _settings.Behavior.AutoStart;
        TopmostMenuItem.IsChecked = _settings.Behavior.AlwaysOnTop;
        RefreshClocks(reposition: true);
        EnforceTopmost();
    }

    private void ApplyAppearance()
    {
        var a = _settings.Appearance;
        RootBorder.Background = ColorHelper.ToBrush(a.Background, Color.FromArgb(0xCC, 0x1E, 0x1E, 0x1E));
        RootBorder.BorderBrush = ColorHelper.ToBrush(a.SeparatorColor, Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
        RootBorder.CornerRadius = new CornerRadius(Math.Max(0, a.CornerRadius));
        RootBorder.Opacity = a.Opacity;
        Height = Math.Max(20, a.BarHeight);
    }

    private void RefreshClocks(bool reposition)
    {
        var lines = _timeService.BuildLines(_settings.Clocks, _settings.Behavior);
        var a = _settings.Appearance;
        var fg = ColorHelper.ToBrush(a.Foreground, Colors.White);
        var sep = ColorHelper.ToBrush(a.SeparatorColor, Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF));
        var font = new FontFamily(a.FontFamily);
        var fontSize = a.FontSize;

        ClocksPanel.Items.Clear();

        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                ClocksPanel.Items.Add(new TextBlock
                {
                    Text = " | ",
                    Foreground = sep,
                    FontFamily = font,
                    FontSize = fontSize,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0)
                });
            }

            var (label, timeText, isValid) = lines[i];
            var block = new TextBlock
            {
                Text = $"{label} {timeText}",
                Foreground = isValid ? fg : Brushes.OrangeRed,
                FontFamily = font,
                FontSize = fontSize,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };
            ClocksPanel.Items.Add(block);
        }

        // Tight width: text only + border padding (avoid large empty space after seconds).
        double textWidth = 0;
        var typeface = new Typeface(font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                var sepFt = new FormattedText(
                    " | ", CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, typeface, fontSize, sep, dpi);
                textWidth += sepFt.WidthIncludingTrailingWhitespace;
            }

            var (label, timeText, _) = lines[i];
            var ft = new FormattedText(
                $"{label} {timeText}", CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, typeface, fontSize, fg, dpi);
            textWidth += ft.WidthIncludingTrailingWhitespace;
        }

        // Padding 6+6 + border 1+1 + 2px safety (anti-clip for bold glyphs)
        var chrome = RootBorder.Padding.Left + RootBorder.Padding.Right
                     + RootBorder.BorderThickness.Left + RootBorder.BorderThickness.Right
                     + 2;
        var contentWidth = Math.Ceiling(textWidth + chrome);
        contentWidth = Math.Max(contentWidth, 40);
        _contentWidth = contentWidth;
        Width = contentWidth;
        Height = Math.Max(20, a.BarHeight);

        if (_isDragging)
            return;

        if (reposition)
        {
            _placement.PlaceBar(this, _settings, contentWidth);
        }
        else
        {
            // Width may change every second — keep position, only clamp to work area.
            _placement.ClampCurrent(this, _settings, contentWidth);
        }
    }

    private void Reposition()
    {
        Width = _contentWidth;
        Height = Math.Max(20, _settings.Appearance.BarHeight);
        _placement.PlaceBar(this, _settings, _contentWidth);
    }

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
        _settingsService.Save(_settings);
        BuildMonitorMenu();
        Reposition();
    }

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
        _settingsWindow.SettingsSaved += (_, updated) =>
        {
            ApplySettings(updated);
        };
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
        // Menu remains for visibility but re-enables if unchecked.
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
            // Keep WPF flag true; Win32 SetWindowPos re-pins Z-order without flicker.
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
