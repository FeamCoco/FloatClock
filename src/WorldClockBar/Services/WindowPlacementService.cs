using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WorldClockBar.Models;
using Forms = System.Windows.Forms;

namespace WorldClockBar.Services;

public sealed class WindowPlacementService
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoactivate = 0x0010;
    private const uint SwpShowwindow = 0x0040;

    public Forms.Screen ResolveScreen(MonitorSettings monitor)
    {
        var screens = Forms.Screen.AllScreens;
        if (screens.Length == 0)
            return Forms.Screen.PrimaryScreen!;

        if (monitor.UsePrimary || string.IsNullOrWhiteSpace(monitor.DeviceName))
            return Forms.Screen.PrimaryScreen ?? screens[0];

        var match = screens.FirstOrDefault(s =>
            string.Equals(s.DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase));
        return match ?? Forms.Screen.PrimaryScreen ?? screens[0];
    }

    /// <summary>
    /// Working area of the screen under the given physical point (or configured monitor).
    /// Excludes the taskbar — this is the free-drag region.
    /// </summary>
    public Forms.Screen ResolveScreenAtPoint(MonitorSettings monitor, int screenX, int screenY)
    {
        var fromPoint = Forms.Screen.FromPoint(new System.Drawing.Point(screenX, screenY));
        if (fromPoint is not null)
            return fromPoint;
        return ResolveScreen(monitor);
    }

    public IReadOnlyList<(string DeviceName, string DisplayName, bool IsPrimary)> ListMonitors()
    {
        return Forms.Screen.AllScreens
            .Select((s, i) => (
                s.DeviceName,
                DisplayName: $"显示器 {i + 1} ({s.Bounds.Width}x{s.Bounds.Height})" + (s.Primary ? " [主]" : ""),
                s.Primary))
            .ToList();
    }

    public double GetDpiScale(Forms.Screen screen)
    {
        var dpi = GetDpiForScreen(screen);
        return Math.Max(1.0, dpi / 96.0);
    }

    /// <summary>
    /// Place using free position or docked alignment. Always stays inside WorkingArea (above taskbar).
    /// </summary>
    public void PlaceBar(Window window, AppSettings settings, double contentWidthDip)
    {
        var screen = ResolveScreen(settings.Monitor);
        var work = screen.WorkingArea;
        var appearance = settings.Appearance;
        var scale = GetDpiScale(screen);

        var heightDip = Math.Max(22, appearance.BarHeight);
        var widthDip = Math.Max(40, contentWidthDip);
        var widthPx = (int)Math.Ceiling(widthDip * scale);
        var heightPx = (int)Math.Ceiling(heightDip * scale);
        widthPx = Math.Min(widthPx, work.Width);
        heightPx = Math.Min(heightPx, Math.Max(20, work.Height));

        int x, y;
        if (appearance.FreePosition)
        {
            x = work.Left + (int)Math.Round(appearance.PosX * scale);
            y = work.Top + (int)Math.Round(appearance.PosY * scale);
        }
        else
        {
            var offsetXPx = (int)Math.Round(appearance.OffsetX * scale);
            var offsetYPx = (int)Math.Round(appearance.OffsetY * scale);
            x = appearance.HorizontalAlignment?.ToLowerInvariant() switch
            {
                "left" => work.Left + offsetXPx,
                "center" => work.Left + (work.Width - widthPx) / 2 + offsetXPx,
                _ => work.Right - widthPx - offsetXPx
            };
            y = work.Bottom - heightPx - offsetYPx;
        }

        (x, y) = ClampToWorkArea(work, x, y, widthPx, heightPx);
        ApplyPixelPosition(window, x, y, widthPx, heightPx, widthDip, heightDip, scale);
    }

    /// <summary>
    /// Move window to pixel position, clamped to the working area of the monitor under that point.
    /// Optionally snap to edges when close enough.
    /// </summary>
    public (int X, int Y, Forms.Screen Screen) DragTo(
        Window window,
        AppSettings settings,
        int desiredLeftPx,
        int desiredTopPx,
        double contentWidthDip,
        bool snapEdges)
    {
        var appearance = settings.Appearance;
        var scaleGuess = GetDpiScale(ResolveScreen(settings.Monitor));
        var widthPx = (int)Math.Ceiling(Math.Max(40, contentWidthDip) * scaleGuess);
        var heightPx = (int)Math.Ceiling(Math.Max(22, appearance.BarHeight) * scaleGuess);

        // Prefer the screen under the center of the bar for multi-monitor drag.
        var centerX = desiredLeftPx + widthPx / 2;
        var centerY = desiredTopPx + heightPx / 2;
        var screen = ResolveScreenAtPoint(settings.Monitor, centerX, centerY);
        var work = screen.WorkingArea;
        var scale = GetDpiScale(screen);

        widthPx = (int)Math.Ceiling(Math.Max(40, contentWidthDip) * scale);
        heightPx = (int)Math.Ceiling(Math.Max(22, appearance.BarHeight) * scale);
        widthPx = Math.Min(widthPx, work.Width);
        heightPx = Math.Min(heightPx, Math.Max(20, work.Height));

        var x = desiredLeftPx;
        var y = desiredTopPx;

        if (snapEdges)
        {
            var snapPx = (int)Math.Round(Math.Max(4, appearance.EdgeSnapDistance) * scale);
            // Left / Right
            if (Math.Abs(x - work.Left) <= snapPx)
                x = work.Left;
            else if (Math.Abs((x + widthPx) - work.Right) <= snapPx)
                x = work.Right - widthPx;

            // Top / Bottom (bottom = just above taskbar)
            if (Math.Abs(y - work.Top) <= snapPx)
                y = work.Top;
            else if (Math.Abs((y + heightPx) - work.Bottom) <= snapPx)
                y = work.Bottom - heightPx;
        }

        (x, y) = ClampToWorkArea(work, x, y, widthPx, heightPx);

        var widthDip = Math.Max(40, contentWidthDip);
        var heightDip = Math.Max(22, appearance.BarHeight);
        ApplyPixelPosition(window, x, y, widthPx, heightPx, widthDip, heightDip, scale);
        return (x, y, screen);
    }

    /// <summary>
    /// Keep current top-left but clamp inside working area (e.g. after resize).
    /// </summary>
    public void ClampCurrent(Window window, AppSettings settings, double contentWidthDip)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            PlaceBar(window, settings, contentWidthDip);
            return;
        }

        GetWindowRect(hwnd, out var rect);
        DragTo(window, settings, rect.Left, rect.Top, contentWidthDip, snapEdges: false);
    }

    public void SaveFreePosition(AppSettings settings, Forms.Screen screen, int leftPx, int topPx)
    {
        var scale = GetDpiScale(screen);
        var work = screen.WorkingArea;
        settings.Appearance.FreePosition = true;
        settings.Appearance.PosX = (leftPx - work.Left) / scale;
        settings.Appearance.PosY = (topPx - work.Top) / scale;
        settings.Monitor.DeviceName = screen.DeviceName;
        settings.Monitor.UsePrimary = screen.Primary;
    }

    public void ClearFreePosition(AppSettings settings)
    {
        settings.Appearance.FreePosition = false;
        settings.Appearance.PosX = 0;
        settings.Appearance.PosY = 0;
    }

    private static (int X, int Y) ClampToWorkArea(
        System.Drawing.Rectangle work, int x, int y, int widthPx, int heightPx)
    {
        // Stay entirely inside working area (taskbar excluded).
        var maxX = work.Right - widthPx;
        var maxY = work.Bottom - heightPx;
        if (maxX < work.Left) maxX = work.Left;
        if (maxY < work.Top) maxY = work.Top;

        x = Math.Max(work.Left, Math.Min(x, maxX));
        y = Math.Max(work.Top, Math.Min(y, maxY));
        return (x, y);
    }

    private static void ApplyPixelPosition(
        Window window,
        int x, int y, int widthPx, int heightPx,
        double widthDip, double heightDip, double scale)
    {
        window.Width = widthDip;
        window.Height = heightDip;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, HwndTopmost, x, y, widthPx, heightPx,
                SwpNoactivate | SwpShowwindow);

            var source = PresentationSource.FromVisual(window);
            if (source?.CompositionTarget is { } ct)
            {
                var topLeft = ct.TransformFromDevice.Transform(new Point(x, y));
                window.Left = topLeft.X;
                window.Top = topLeft.Y;
            }
            else
            {
                window.Left = x / scale;
                window.Top = y / scale;
            }
        }
        else
        {
            window.Left = x / scale;
            window.Top = y / scale;
        }
    }

    private static double GetDpiForScreen(Forms.Screen screen)
    {
        try
        {
            var mon = MonitorFromPoint(
                new System.Drawing.Point(
                    screen.Bounds.Left + screen.Bounds.Width / 2,
                    screen.Bounds.Top + screen.Bounds.Height / 2),
                2);

            if (mon != IntPtr.Zero &&
                GetDpiForMonitor(mon, 0, out var dpiX, out _) == 0 &&
                dpiX > 0)
            {
                return dpiX;
            }
        }
        catch
        {
            // fall through
        }

        return 96.0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RectPx lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint dwFlags);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectPx
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
