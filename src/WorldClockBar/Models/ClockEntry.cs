namespace WorldClockBar.Models;

public sealed class ClockEntry
{
    public string Label { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "UTC";
}
