using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace WorldClockBar.Services;

/// <summary>
/// System tray icon so the app can be found even if the bar is off-screen.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notify;
    private bool _disposed;

    public event Action? OpenSettingsRequested;
    public event Action? ShowBarRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        _notify = new Forms.NotifyIcon
        {
            Visible = true,
            Text = "WorldClockBar — 多时区时钟",
            Icon = SystemIcons.Application
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示时钟条", null, (_, _) => ShowBarRequested?.Invoke());
        menu.Items.Add("设置...", null, (_, _) => OpenSettingsRequested?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke());
        _notify.ContextMenuStrip = menu;
        _notify.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke();
    }

    public void ShowBalloon(string title, string text)
    {
        try
        {
            _notify.BalloonTipTitle = title;
            _notify.BalloonTipText = text;
            _notify.ShowBalloonTip(3000);
        }
        catch
        {
            // ignore
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notify.Visible = false;
        _notify.Dispose();
    }
}
