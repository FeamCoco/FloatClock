using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace WorldClockBar.Services;

/// <summary>
/// Fluent theme infrastructure: system dark/light + accent detection, app-wide brushes,
/// DWM Mica backdrop for normal windows and Acrylic blur-behind for the clock bar.
/// </summary>
public static class ThemeService
{
    // ---------------------------------------------------------------- state

    private static bool _hooksInstalled;
    private static bool _isDark;

    /// <summary>Raised when the OS light/dark preference flips.</summary>
    public static event Action? SystemThemeChanged;

    public static bool IsDarkTheme => _isDark;

    /// <summary>Win11 22H2+ exposes DWMWA_SYSTEMBACKDROP_TYPE.</summary>
    public static bool SupportsMica =>
        Environment.OSVersion.Version >= new Version(10, 0, 22621);

    // ------------------------------------------------------------ palettes

    public sealed record BarPalette(string Name, string Background, string Foreground, string SeparatorColor);

    public static readonly IReadOnlyList<BarPalette> BarPalettes = new[]
    {
        new BarPalette("System",       "#E9FAFBFC", "#FF1B1B1B", "#24000000"),
        new BarPalette("Light",        "#E9FAFBFC", "#FF1B1B1B", "#24000000"),
        new BarPalette("Dark",         "#CC212124", "#FFF3F5F8", "#2BFFFFFF"),
        new BarPalette("HighContrast", "#FF000000", "#FFFFE000", "#80FFFFFF"),
        new BarPalette("MistBlue",     "#E6DCE9F5", "#FF2A4158", "#332A4158"),
        new BarPalette("WarmSand",     "#E6F2E4D2", "#FF5C4326", "#335C4326"),
        new BarPalette("Glass",        "#99FFFFFF", "#FF33343D", "#3333343D"),
    };

    public static string SystemThemeName => _isDark ? "Dark" : "Light";

    /// <summary>
    /// Resolve the effective bar palette for a theme name. "System" maps to the live OS theme.
    /// </summary>
    public static BarPalette ResolveBarPalette(string? themeName)
    {
        var name = string.IsNullOrWhiteSpace(themeName) ? "System" : themeName!;
        if (string.Equals(name, "System", StringComparison.OrdinalIgnoreCase))
            name = _isDark ? "Dark" : "Light";

        var match = BarPalettes.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        return match ?? BarPalettes[1];
    }

    // ------------------------------------------------------- theme detection

    public static bool ReadSystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int light)
                return light == 0;
        }
        catch
        {
            // fall through
        }
        return false;
    }

    /// <summary>System accent color, decoded from the DWM registry value; falls back to Win11 blue.</summary>
    public static Color GetAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("ColorizationColor") is int abgr)
            {
                // Stored as little-endian AABBGGRR.
                var b = BitConverter.GetBytes(abgr);
                var c = Color.FromArgb(0xFF, b[0], b[1], b[2]);
                // Sanity: reject near-black/white garbage, keep hue only when plausible.
                if (c.R + c.G + c.B > 40 && c.R + c.G + c.B < 740)
                    return c;
            }
        }
        catch
        {
            // fall through
        }
        return Color.FromRgb(0x00, 0x67, 0xC0);
    }

    /// <summary>Install app-wide Fluent brushes and OS theme hooks. Call once at startup.</summary>
    public static void Initialize()
    {
        ApplyAppTheme();

        if (_hooksInstalled)
            return;
        _hooksInstalled = true;

        try
        {
            SystemEvents.UserPreferenceChanged += (_, args) =>
            {
                if (args.Category != UserPreferenceCategory.General)
                    return;
                var wasDark = _isDark;
                ApplyAppTheme();
                if (wasDark != _isDark)
                    SystemThemeChanged?.Invoke();
            };
        }
        catch
        {
            // SystemEvents unavailable — theme just won't live-switch.
        }
    }

    /// <summary>Write light/dark brush tokens into application resources.</summary>
    public static void ApplyAppTheme()
    {
        _isDark = ReadSystemPrefersDark();
        var accent = GetAccentColor();

        // On dark surfaces Win11 lifts the accent for contrast.
        var accentBright = _isDark && accent is { R: < 120, G: < 160, B: < 220 }
            ? Color.FromRgb(
                (byte)Math.Min(255, accent.R + 110),
                (byte)Math.Min(255, accent.G + 110),
                (byte)Math.Min(255, accent.B + 110))
            : accent;

        var res = Application.Current.Resources;
        void Set(string key, Color c) =>
            res[key] = new SolidColorBrush(c);

        if (_isDark)
        {
            // 方案二「雾层微抬升」：三阶不透明色阶（窗口 → 卡片 → 控件，逐层亮一档）。
            // 表面一律不用半透明白——背板未接管时半透明会直接压在黑底上，渲染成黑色块。
            Set("Fluent.WindowBg", From("#FF1C1C20"));
            Set("Fluent.CardBg", From("#FF28282D"));
            Set("Fluent.CardBgStrong", From("#FF2E2E34"));
            Set("Fluent.CardBorder", From("#0FFFFFFF"));
            Set("Fluent.Divider", From("#0FFFFFFF"));
            Set("Fluent.TextPrimary", From("#FFF2F3F6"));
            Set("Fluent.TextSecondary", From("#FFA9AEB8"));
            Set("Fluent.TextTertiary", From("#FF6F747D"));
            Set("Fluent.Accent", accentBright);
            Set("Fluent.AccentText", From("#FF0C2733"));
            Set("Fluent.SubtleHover", From("#0DFFFFFF"));
            Set("Fluent.SubtlePressed", From("#08FFFFFF"));
            Set("Fluent.ControlBg", From("#FF38383F"));
            Set("Fluent.ControlBorder", From("#14FFFFFF"));
            Set("Fluent.NavBg", From("#06FFFFFF"));
            Set("Fluent.SurfaceSolid", From("#FF2A2A30"));
            Set("Fluent.RowHover", From("#14FFFFFF"));
            Set("Fluent.RowSelected", From("#1FFFFFFF"));
            Set("Fluent.Inactive", From("#FF8A8F98"));
            Set("Fluent.SwitchOff", From("#FF57575F"));
            Set("Fluent.TrackBg", From("#40FFFFFF"));
        }
        else
        {
            // 浅色同样用不透明表面，杜绝同一类背板回退问题。
            Set("Fluent.WindowBg", From("#F3F3F3"));
            Set("Fluent.CardBg", From("#FFFBFBFC"));
            Set("Fluent.CardBgStrong", From("#FFFFFFFF"));
            Set("Fluent.CardBorder", From("#0F000000"));
            Set("Fluent.Divider", From("#12000000"));
            Set("Fluent.TextPrimary", From("#FF1B1B1B"));
            Set("Fluent.TextSecondary", From("#FF5D6470"));
            Set("Fluent.TextTertiary", From("#FF8A9099"));
            Set("Fluent.Accent", accent);
            Set("Fluent.AccentText", From("#FFFFFFFF"));
            Set("Fluent.SubtleHover", From("#0A000000"));
            Set("Fluent.SubtlePressed", From("#05000000"));
            Set("Fluent.ControlBg", From("#FFFFFFFF"));
            Set("Fluent.ControlBorder", From("#14000000"));
            Set("Fluent.NavBg", From("#66FFFFFF"));
            Set("Fluent.SurfaceSolid", From("#FFFAFAFB"));
            Set("Fluent.RowHover", From("#0F000000"));
            Set("Fluent.RowSelected", From("#17000000"));
            Set("Fluent.Inactive", From("#FF9A9FA8"));
            Set("Fluent.SwitchOff", From("#59000000"));
            Set("Fluent.TrackBg", From("#33000000"));
        }

        Set("Fluent.Error", From("#FFC42B1C"));
        Set("Fluent.Success", From("#FF0F7B0F"));
        Set("Fluent.CloseHover", From("#FFC42B1C"));
    }

    private static Color From(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    // ------------------------------------------------------------ DWM: Mica

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaSystembackdropType = 38;
    private const int DmwsbtMica = 2;

    /// <summary>
    /// Enable the Mica backdrop on a normal window. Uses DwmExtendFrameIntoClientArea
    /// (paint-only) rather than WindowChrome.GlassFrameThickness=-1, which would make
    /// the whole client area system-draggable. Requires a transparent window background.
    /// Returns false (leaving the solid WindowBg brush in place) when unsupported.
    /// </summary>
    public static bool TryApplyMica(Window window, bool dark)
    {
        if (!SupportsMica)
            return false;

        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return false;

            var darkVal = dark ? 1 : 0;
            _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkVal, sizeof(int));

            var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            if (DwmExtendFrameIntoClientArea(hwnd, ref margins) != 0)
                return false;

            var backdrop = DmwsbtMica;
            var hr = DwmSetWindowAttribute(hwnd, DwmwaSystembackdropType, ref backdrop, sizeof(int));
            if (hr != 0)
                return false;

            window.Background = Brushes.Transparent;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------- Acrylic (clock bar)

    private const int WcaAccentPolicy = 19;
    private const int AccentDisabled = 0;
    private const int AccentEnableAcrylicBlurbehind = 4;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmcpRound = 2;
    private const int DmwsbtNone = 1;
    private const int DmwsbtAcrylic = 3; // DWMSBT_TRANSIENTWINDOW

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    /// <summary>
    /// Draw an acrylic blur behind the window, tinted with the given color (alpha controls strength).
    /// WPF content should keep a transparent (or near-transparent) background where the blur shows.
    /// </summary>
    public static bool TrySetAcrylic(Window window, Color tint)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return false;

            var policy = new AccentPolicy
            {
                AccentState = AccentEnableAcrylicBlurbehind,
                AccentFlags = 2,
                // DWM expects AABBGGRR.
                GradientColor = (uint)(tint.A << 24 | tint.B << 16 | tint.G << 8 | tint.R)
            };

            var size = Marshal.SizeOf<AccentPolicy>();
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WcaAccentPolicy,
                    Data = ptr,
                    SizeOfData = size
                };
                return SetWindowCompositionAttribute(hwnd, ref data) != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Remove the acrylic effect (fall back to plain WPF rendering).</summary>
    public static void ClearAcrylic(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            var policy = new AccentPolicy { AccentState = AccentDisabled };
            var size = Marshal.SizeOf<AccentPolicy>();
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WcaAccentPolicy,
                    Data = ptr,
                    SizeOfData = size
                };
                _ = SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// Enable the Win11 22H2+ system Acrylic backdrop (DWMSBT_TRANSIENTWINDOW) behind a
    /// non-layered window. This is the only path that produces real blur on current
    /// Win11 builds — the legacy SetWindowCompositionAttribute accent renders as a
    /// flat solid there. The window must paint transparent pixels where the material
    /// should show. Returns false when unsupported (caller falls back to accent/solid).
    /// </summary>
    public static bool TrySetSystemAcrylic(Window window, bool dark)
    {
        if (!SupportsMica)
            return false;

        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return false;

            var darkVal = dark ? 1 : 0;
            _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkVal, sizeof(int));

            var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            if (DwmExtendFrameIntoClientArea(hwnd, ref margins) != 0)
                return false;

            var backdrop = DmwsbtAcrylic;
            if (DwmSetWindowAttribute(hwnd, DwmwaSystembackdropType, ref backdrop, sizeof(int)) != 0)
                return false;

            // WS_POPUP tool windows are not auto-rounded; ask DWM explicitly.
            var round = DwmcpRound;
            _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref round, sizeof(int));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Turn the system backdrop off (used when the bar falls back to solid).</summary>
    public static void ClearSystemBackdrop(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;
            var none = DmwsbtNone;
            _ = DwmSetWindowAttribute(hwnd, DwmwaSystembackdropType, ref none, sizeof(int));
        }
        catch
        {
            // ignore
        }
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
}
