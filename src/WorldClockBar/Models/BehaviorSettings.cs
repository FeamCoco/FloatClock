namespace WorldClockBar.Models;

public sealed class BehaviorSettings
{
    public bool AutoStart { get; set; }
    public bool ShowSeconds { get; set; } = true;
    /// <summary>Keep the bar above other windows (re-asserted periodically).</summary>
    public bool AlwaysOnTop { get; set; } = true;
    public string TimeFormat { get; set; } = "HH:mm:ss";
    public string? DateFormat { get; set; }
}
