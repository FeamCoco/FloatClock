using System.Windows.Media;

namespace WorldClockBar.Services;

public static class ColorHelper
{
    public static Color Parse(string? text, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        try
        {
            return (Color)ColorConverter.ConvertFromString(text.Trim());
        }
        catch
        {
            return fallback;
        }
    }

    public static Brush ToBrush(string? text, Color fallback)
    {
        var color = Parse(text, fallback);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public static string ToHex(Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static string ToHex(Brush? brush, string fallback)
    {
        if (brush is SolidColorBrush solid)
            return ToHex(solid.Color);
        return fallback;
    }
}
