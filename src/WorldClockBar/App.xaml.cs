using System.Threading;
using System.Windows;
using WorldClockBar.Services;

namespace WorldClockBar;

public partial class App : Application
{
    private const string MutexName = "Local\\WorldClockBar_SingleInstance_Mutex";
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "WorldClockBar 已在运行。",
                "WorldClockBar",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var settingsService = new SettingsService();
        // --reset 或环境变量可恢复出厂：英国 + 白色主题
        var reset = e.Args.Any(a => string.Equals(a, "--reset", StringComparison.OrdinalIgnoreCase))
                    || string.Equals(Environment.GetEnvironmentVariable("WORLDCLOCKBAR_RESET"), "1", StringComparison.Ordinal);
        var settings = reset ? settingsService.ResetToDefaults() : settingsService.Load();

        // Sync autostart flag with registry on launch.
        var autostart = new AutostartService();
        if (settings.Behavior.AutoStart != autostart.IsEnabled())
        {
            try { autostart.SetEnabled(settings.Behavior.AutoStart); }
            catch { /* ignore registry permission issues */ }
        }

        var main = new MainWindow(settingsService, settings);
        MainWindow = main;
        main.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        catch
        {
            // ignore
        }

        base.OnExit(e);
    }
}
