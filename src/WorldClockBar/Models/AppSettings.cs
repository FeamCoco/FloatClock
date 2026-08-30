namespace WorldClockBar.Models;

public sealed class AppSettings
{
    public List<ClockEntry> Clocks { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public MonitorSettings Monitor { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();

    public static AppSettings CreateDefault() => new()
    {
        Clocks =
        [
            new ClockEntry { Label = "英国", TimeZoneId = "GMT Standard Time" }
        ],
        Appearance = new AppearanceSettings
        {
            ThemeName = "System",
            Background = "#E9FAFBFC",
            Foreground = "#FF1B1B1B",
            SeparatorColor = "#24000000",
            FontFamily = "Segoe UI Variable Display, Segoe UI",
            FontSize = 15,
            Opacity = 1.0,
            BarHeight = 40,
            CornerRadius = 8,
            HorizontalAlignment = "Right",
            OffsetX = 4,
            OffsetY = 2,
            FreePosition = false,
            EdgeSnapDistance = 14
        },
        Monitor = new MonitorSettings(),
        Behavior = new BehaviorSettings
        {
            AutoStart = false,
            ShowSeconds = true,
            AlwaysOnTop = true,
            TimeFormat = "HH:mm:ss"
        }
    };
}
