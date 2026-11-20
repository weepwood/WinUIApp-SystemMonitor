using System;
using System.IO;
using System.Text.Json;

namespace WinUIApp.Services;

// LocalAppData also works in the unpackaged desktop build (no package identity required).
public sealed class MonitorPreferences
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinUIApp-SystemMonitor", "preferences.json");

    public string Theme { get; set; } = "System";
    public int IntervalSeconds { get; set; } = 1;
    public bool AlwaysOnTop { get; set; }

    public static MonitorPreferences Load()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<MonitorPreferences>(File.ReadAllText(FilePath)) ?? new();
            if (settings.Theme is not ("Light" or "Dark")) settings.Theme = "System";
            if (settings.IntervalSeconds is not (1 or 2 or 5)) settings.IntervalSeconds = 1;
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this));
            File.Move(FilePath + ".tmp", FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
