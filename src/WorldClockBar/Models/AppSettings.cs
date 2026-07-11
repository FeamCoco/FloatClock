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
            Background = "#F2FFFFFF",
            Foreground = "#FF111111",
            SeparatorColor = "#66111111",
            FontFamily = "Segoe UI",
            FontSize = 14,
            Opacity = 1.0,
            BarHeight = 32,
            CornerRadius = 6,
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
