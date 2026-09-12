using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorldClockBar.Models;

namespace WorldClockBar.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WorldClockBar");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    public string SettingsPath => _settingsPath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                var defaults = AppSettings.CreateDefault();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is null)
                return AppSettings.CreateDefault();

            Normalize(settings);
            return settings;
        }
        catch
        {
            return AppSettings.CreateDefault();
        }
    }

    /// <summary>
    /// Overwrite config with factory defaults (used when resetting for the user).
    /// </summary>
    public AppSettings ResetToDefaults()
    {
        var defaults = AppSettings.CreateDefault();
        Save(defaults);
        return defaults;
    }

    public void Save(AppSettings settings)
    {
        Normalize(settings);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private static void Normalize(AppSettings settings)
    {
        settings.Clocks ??= [];
        settings.Appearance ??= new AppearanceSettings();
        settings.Monitor ??= new MonitorSettings();
        settings.Behavior ??= new BehaviorSettings();

        if (settings.Clocks.Count == 0)
        {
            settings.Clocks.AddRange(AppSettings.CreateDefault().Clocks);
        }

        var a = settings.Appearance;
        if (a.FontSize < 12) a.FontSize = 12;
        if (a.FontSize > 32) a.FontSize = 32;
        // The bar holds two stacked rows since brand v2 (§5.1), so its minimum height is
        // derived from the time font size — a pre-v2 config with barHeight 40 would otherwise
        // squeeze the city name into the big time. Derive after the font size is settled.
        var minBarHeight = AppearanceSettings.MinBarHeight(a.FontSize);
        if (a.BarHeight < minBarHeight) a.BarHeight = minBarHeight;
        if (a.BarHeight > 96) a.BarHeight = 96;
        if (a.Opacity < 0.2) a.Opacity = 0.2;
        if (a.Opacity > 1.0) a.Opacity = 1.0;
        if (string.IsNullOrWhiteSpace(a.FontFamily)) a.FontFamily = "Segoe UI";
        if (string.IsNullOrWhiteSpace(a.HorizontalAlignment)) a.HorizontalAlignment = "Right";

        // 品牌规格 §7：雾蓝 / 暖砂 / 玻璃三套装饰性预设已删除，旧配置就近迁到品牌浅色面。
        if (IsRetiredBarPreset(a.ThemeName))
            a.ThemeName = "Light";

        var b = settings.Behavior;
        if (string.IsNullOrWhiteSpace(b.TimeFormat))
            b.TimeFormat = b.ShowSeconds ? "HH:mm:ss" : "HH:mm";
    }

    /// <summary>Presets that existed before brand v2 and were dropped in §7.</summary>
    private static bool IsRetiredBarPreset(string? themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
            return false;
        return themeName.Equals("MistBlue", StringComparison.OrdinalIgnoreCase)
               || themeName.Equals("WarmSand", StringComparison.OrdinalIgnoreCase)
               || themeName.Equals("Glass", StringComparison.OrdinalIgnoreCase);
    }
}
