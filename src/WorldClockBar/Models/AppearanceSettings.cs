namespace WorldClockBar.Models;

public sealed class AppearanceSettings
{
    /// <summary>
    /// System | Light | Dark | HighContrast | Custom.
    /// Selecting a preset fills Background/Foreground/SeparatorColor; System follows the OS theme.
    /// (The decorative MistBlue/WarmSand/Glass presets were removed in the brand v2 spec §7;
    /// a config still holding one of those names falls back to the Light face.)
    /// </summary>
    public string ThemeName { get; set; } = "System";

    public string Background { get; set; } = "#EBFFFFFF";
    public string Foreground { get; set; } = "#FF14201E";

    /// <summary>
    /// Kept for config round-tripping only — §5.1 removed the 1px divider between cities,
    /// so nothing renders this colour any more. Do not wire new UI to it.
    /// </summary>
    public string SeparatorColor { get; set; } = "#24000000";

    public string FontFamily { get; set; } = "Segoe UI Variable Display, Segoe UI";
    public double FontSize { get; set; } = 19;
    public double Opacity { get; set; } = 1.0;
    public double BarHeight { get; set; } = 60;
    public double CornerRadius { get; set; } = 12;

    /// <summary>Left | Center | Right — used when FreePosition is false.</summary>
    public string HorizontalAlignment { get; set; } = "Right";

    /// <summary>Margin from the aligned edge when docked (DIPs). Right dock uses this as right margin.</summary>
    public double OffsetX { get; set; } = 4;

    /// <summary>Margin from bottom when docked (DIPs).</summary>
    public double OffsetY { get; set; } = 2;

    /// <summary>When true, PosX/PosY place the bar freely inside the working area.</summary>
    public bool FreePosition { get; set; }

    /// <summary>Left edge relative to working-area left (DIPs).</summary>
    public double PosX { get; set; }

    /// <summary>Top edge relative to working-area top (DIPs).</summary>
    public double PosY { get; set; }

    /// <summary>Distance (DIPs) within which the bar snaps to work-area edges.</summary>
    public double EdgeSnapDistance { get; set; } = 14;

    /// <summary>
    /// Smallest bar height that still fits one city group at the given time font size.
    /// The group is two stacked rows since brand v2 (§5.1): city name (≈1.34 × 11) + 2px row
    /// gap + the time row (≈1.34 × fontSize) + 8px of group padding + 1px slack.
    /// Below this the two rows collide and the window clips them.
    /// </summary>
    public static double MinBarHeight(double fontSize) =>
        Math.Ceiling(fontSize * 1.34 + 26);
}
