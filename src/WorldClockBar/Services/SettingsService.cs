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
        if (a.FontSize < 8) a.FontSize = 8;
        if (a.FontSize > 48) a.FontSize = 48;
        if (a.BarHeight < 20) a.BarHeight = 20;
        if (a.BarHeight > 80) a.BarHeight = 80;
        if (a.Opacity < 0.2) a.Opacity = 0.2;
        if (a.Opacity > 1.0) a.Opacity = 1.0;
        if (string.IsNullOrWhiteSpace(a.FontFamily)) a.FontFamily = "Segoe UI";
        if (string.IsNullOrWhiteSpace(a.HorizontalAlignment)) a.HorizontalAlignment = "Right";

        var b = settings.Behavior;
        if (string.IsNullOrWhiteSpace(b.TimeFormat))
            b.TimeFormat = b.ShowSeconds ? "HH:mm:ss" : "HH:mm";
    }
}
