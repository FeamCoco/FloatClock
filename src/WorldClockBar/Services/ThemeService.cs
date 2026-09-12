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

    /// <summary>
    /// Clock-bar presets. Brand spec §2.4 / §7: the decorative palettes (雾蓝 / 暖砂 / 玻璃)
    /// were dropped — the bar now has exactly two brand faces plus a system follow and a
    /// high-contrast escape hatch. Values are #AARRGGBB; the bar alpha comes from §2.4
    /// <c>barBg</c> (light 92% white, dark 86% #111817).
    /// </summary>
    public static readonly IReadOnlyList<BarPalette> BarPalettes = new[]
    {
        new BarPalette("System",       "#EBFFFFFF", "#FF14201E", "#24000000"),
        new BarPalette("Light",        "#EBFFFFFF", "#FF14201E", "#24000000"),
        new BarPalette("Dark",         "#DB111817", "#FFEAF3F1", "#2BFFFFFF"),
        new BarPalette("HighContrast", "#FF000000", "#FFFFE000", "#80FFFFFF"),
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

    /// <summary>
    /// Whether the taskbar itself is dark — this is <c>SystemUsesLightTheme</c>, a separate
    /// registry value from the app theme. The brand spec §4 calls out that the tray icon must
    /// match the taskbar, not the app, because a customised taskbar colour can disagree.
    /// </summary>
    public static bool IsTaskbarDark
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("SystemUsesLightTheme") is int light)
                    return light == 0;
            }
            catch
            {
                // fall through
            }
            return _isDark;
        }
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

    /// <summary>
    /// Write light/dark brush tokens into application resources. Every value is taken
    /// from the brand spec §2.4 role map, so the settings window, the switches, the links
    /// and the bar's "local city" dot all resolve to the same Meridian teal.
    /// The system accent is deliberately no longer consulted — see §2.4's closing note.
    /// </summary>
    public static void ApplyAppTheme()
    {
        _isDark = ReadSystemPrefersDark();

        var res = Application.Current.Resources;
        void Set(string key, Color c) =>
            res[key] = new SolidColorBrush(c);

        if (_isDark)
        {
            // §2.4 dark column: cyan-tinted neutrals, never pure grey — pure grey next to
            // the teal reads as dirty yellow, which is the easiest way to wreck this palette.
            Set("Fluent.WindowBg", From("#111817"));
            Set("Fluent.CardBg", From("#1A2220"));
            Set("Fluent.CardBgStrong", From("#222C2A"));
            Set("Fluent.CardBorder", From("#2A3432"));
            Set("Fluent.Divider", From("#222C2A"));
            Set("Fluent.TextPrimary", From("#EAF3F1"));
            Set("Fluent.TextSecondary", From("#9FB0AD"));
            Set("Fluent.TextTertiary", From("#6E807D"));
            Set("Fluent.Accent", From("#3AAFAA"));
            Set("Fluent.AccentHover", From("#6FCEC6"));
            Set("Fluent.AccentText", From("#04211F"));
            Set("Fluent.ControlBg", From("#273130"));
            Set("Fluent.ControlBorder", From("#2A3432"));
            Set("Fluent.NavBg", From("#161D1C"));
            Set("Fluent.SurfaceSolid", From("#222C2A"));
            Set("Fluent.SubtleHover", From("#0DFFFFFF"));
            Set("Fluent.SubtlePressed", From("#08FFFFFF"));
            Set("Fluent.RowHover", From("#14FFFFFF"));
            // Selection is a brand tint rather than a grey film — §6.2 asks for 浅底 + 主色文字.
            Set("Fluent.RowSelected", Color.FromArgb(0x33, 0x3A, 0xAF, 0xAA));
            Set("Fluent.Inactive", From("#6E807D"));
            Set("Fluent.SwitchOff", From("#3A4746"));
            Set("Fluent.TrackBg", From("#33FFFFFF"));
        }
        else
        {
            Set("Fluent.WindowBg", From("#F2F5F5"));
            Set("Fluent.CardBg", From("#FFFFFF"));
            Set("Fluent.CardBgStrong", From("#FFFFFF"));
            Set("Fluent.CardBorder", From("#E2E8E8"));
            Set("Fluent.Divider", From("#EDF2F2"));
            Set("Fluent.TextPrimary", From("#14201E"));
            Set("Fluent.TextSecondary", From("#526260"));
            Set("Fluent.TextTertiary", From("#8A9A98"));
            Set("Fluent.Accent", From("#0F766E"));
            Set("Fluent.AccentHover", From("#178B86"));
            Set("Fluent.AccentText", From("#FFFFFF"));
            Set("Fluent.ControlBg", From("#FFFFFF"));
            Set("Fluent.ControlBorder", From("#E2E8E8"));
            Set("Fluent.NavBg", From("#EBF0F0"));
            Set("Fluent.SurfaceSolid", From("#FFFFFF"));
            Set("Fluent.SubtleHover", From("#0A000000"));
            Set("Fluent.SubtlePressed", From("#05000000"));
            Set("Fluent.RowHover", From("#0F000000"));
            Set("Fluent.RowSelected", Color.FromArgb(0x2E, 0x0F, 0x76, 0x6E));
            Set("Fluent.Inactive", From("#8A9A98"));
            Set("Fluent.SwitchOff", From("#59000000"));
            Set("Fluent.TrackBg", From("#26000000"));
        }

        Set("Fluent.Error", From("#C42B1C"));
        Set("Fluent.Success", From("#0F7B0F"));
        Set("Fluent.Warning", From("#C77700"));
        Set("Fluent.CloseHover", From("#C42B1C"));
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
