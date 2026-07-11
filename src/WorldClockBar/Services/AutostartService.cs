using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace WorldClockBar.Services;

public sealed class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WorldClockBar";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = key?.GetValue(ValueName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (key is null)
            return;

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            return;

        key.SetValue(ValueName, $"\"{exePath}\"");
    }

    private static string ResolveExecutablePath()
    {
        // Prefer process path (works for published single-file / framework-dependent).
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            return processPath;

        var mainModule = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(mainModule) && File.Exists(mainModule))
            return mainModule;

        return Path.Combine(AppContext.BaseDirectory, "WorldClockBar.exe");
    }
}
