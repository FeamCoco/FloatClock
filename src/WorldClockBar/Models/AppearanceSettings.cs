namespace WorldClockBar.Models;

public sealed class AppearanceSettings
{
    public string Background { get; set; } = "#F2FFFFFF";
    public string Foreground { get; set; } = "#FF111111";
    public string SeparatorColor { get; set; } = "#66111111";
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 14;
    public double Opacity { get; set; } = 1.0;
    public double BarHeight { get; set; } = 32;
    public double CornerRadius { get; set; } = 6;

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
}
