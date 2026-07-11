using WorldClockBar.Models;

namespace WorldClockBar.Services;

public sealed class TimeDisplayService
{
    private readonly Dictionary<string, TimeZoneInfo> _cache = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<(string Label, string TimeText, bool IsValid)> BuildLines(
        IEnumerable<ClockEntry> clocks,
        BehaviorSettings behavior)
    {
        var utcNow = DateTime.UtcNow;
        var format = ResolveFormat(behavior);
        var result = new List<(string, string, bool)>();

        foreach (var clock in clocks)
        {
            var label = string.IsNullOrWhiteSpace(clock.Label) ? clock.TimeZoneId : clock.Label.Trim();
            if (!TryGetZone(clock.TimeZoneId, out var zone))
            {
                result.Add((label, "--:--:--", false));
                continue;
            }

            var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone);
            var timeText = local.ToString(format);

            if (!string.IsNullOrWhiteSpace(behavior.DateFormat))
            {
                timeText = local.ToString(behavior.DateFormat) + " " + timeText;
            }

            result.Add((label, timeText, true));
        }

        return result;
    }

    public static IReadOnlyList<TimeZoneInfo> GetSystemTimeZones()
        => TimeZoneInfo.GetSystemTimeZones().OrderBy(z => z.DisplayName).ToList();

    private static string ResolveFormat(BehaviorSettings behavior)
    {
        if (!string.IsNullOrWhiteSpace(behavior.TimeFormat))
            return behavior.TimeFormat;

        return behavior.ShowSeconds ? "HH:mm:ss" : "HH:mm";
    }

    private bool TryGetZone(string? id, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (_cache.TryGetValue(id, out zone!))
            return true;

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            _cache[id] = zone;
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
