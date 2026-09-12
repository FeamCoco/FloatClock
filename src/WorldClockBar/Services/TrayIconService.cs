using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace WorldClockBar.Services;

/// <summary>
/// System tray icon so the app can be found even if the bar is off-screen.
/// The icon is the brand mark (brand spec §4): the shipping build used to show the
/// generic <c>SystemIcons.Application</c>, which had nothing to do with the product.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const string WhiteIcon = "tray-white.ico";
    private const string InkIcon = "tray-ink.ico";

    private readonly Forms.NotifyIcon _notify;
    private Icon? _icon;                 // owned: came from an embedded resource
    private bool _usingFallback;         // true only while showing the shared system icon
    private bool _disposed;

    public event Action? OpenSettingsRequested;
    public event Action? ShowBarRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        _notify = new Forms.NotifyIcon
        {
            Visible = true,
            Text = "WorldClockBar — 多时区时钟"
        };
        ApplyVariant();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示时钟条", null, (_, _) => ShowBarRequested?.Invoke());
        menu.Items.Add("设置...", null, (_, _) => OpenSettingsRequested?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke());
        _notify.ContextMenuStrip = menu;
        _notify.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke();
    }

    /// <summary>
    /// §4: white on a dark taskbar, ink on a light one. The light case intentionally gets
    /// ink rather than brand teal — a customised taskbar colour can be any shade, and ink
    /// holds contrast regardless, whereas teal depends on the brand colour staying legible.
    /// </summary>
    private static string CurrentVariant() => ThemeService.IsTaskbarDark ? WhiteIcon : InkIcon;

    /// <summary>Re-evaluate the variant after the taskbar switches between light and dark.</summary>
    public void RefreshIcon()
    {
        if (_disposed)
            return;
        ApplyVariant();
    }

    private void ApplyVariant()
    {
        var loaded = LoadIcon(CurrentVariant());
        var previous = _icon;
        var previousWasFallback = _usingFallback;

        if (loaded is not null)
        {
            _icon = loaded;
            _usingFallback = false;
            _notify.Icon = loaded;
        }
        else
        {
            // Last resort only — the embedded brand icon could not be read. A tray entry with
            // no icon at all would be worse, but a normal build never reaches this branch.
            _icon = null;
            _usingFallback = true;
            _notify.Icon = SystemIcons.Application;
        }

        if (!previousWasFallback)
            previous?.Dispose();
    }

    private static Icon? LoadIcon(string fileName)
    {
        try
        {
            var assembly = typeof(TrayIconService).Assembly;
            // Match on the suffix rather than hard-coding the manifest resource name, so a
            // change to the root namespace or the Assets layout cannot silently break the tray.
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (resourceName is null)
                return null;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                return null;

            // Let the shell pick the DPI-appropriate frame instead of forcing 16×16; each
            // file carries its own 16/20/24/32 geometry.
            return new Icon(stream, Forms.SystemInformation.SmallIconSize);
        }
        catch
        {
            return null;
        }
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
        _notify.Icon = null;
        _notify.Dispose();
        if (!_usingFallback)
            _icon?.Dispose();
        _icon = null;
    }
}
