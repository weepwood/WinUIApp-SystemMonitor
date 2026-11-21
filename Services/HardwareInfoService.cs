using System;
using System.Collections.Generic;
using System.Management;

namespace WinUIApp.Services;

/// <summary>
/// WMI access returns detached value snapshots, disposing native WMI handles promptly.
/// This is important for a long-running monitoring application.
/// </summary>
public static class HardwareInfoService
{
    private static readonly ManagementScope Scope = new(@"\\.\ROOT\CIMV2");

    public static List<Dictionary<string, object?>> ExecuteQuery(string query)
    {
        var results = new List<Dictionary<string, object?>>();
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, new ObjectQuery(query));
            using var collection = searcher.Get();
            foreach (ManagementObject obj in collection)
            {
                using (obj)
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (PropertyData property in obj.Properties)
                        row[property.Name] = property.Value;
                    results.Add(row);
                }
            }
        }
        catch (Exception)
        {
            // WMI is optional; callers can display unavailable metrics as zero/unknown.
        }
        return results;
    }

    public static T? GetPropertyValue<T>(IReadOnlyDictionary<string, object?> obj, string propertyName)
        where T : struct
    {
        if (!obj.TryGetValue(propertyName, out object? value) || value is null)
            return null;
        if (value is T typedValue)
            return typedValue;
        try
        {
            // Some WMI providers expose compatible counter widths (UInt16/UInt32/UInt64).
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string GetStringProperty(IReadOnlyDictionary<string, object?> obj, string propertyName)
    {
        return obj.TryGetValue(propertyName, out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
    }

    public static double BytesToGB(ulong bytes) => Math.Round(bytes / (1024.0 * 1024.0 * 1024.0), 2);
    public static double BytesToGB(long bytes) => Math.Round(bytes / (1024.0 * 1024.0 * 1024.0), 2);
    public static double BytesToMB(long bytes) => Math.Round(bytes / (1024.0 * 1024.0), 2);
    public static double BitsToMbps(long bits) => Math.Round(bits / 1_000_000.0, 2);

    public static string FormatBytes(ulong bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    public static string FormatSpeed(double mbps)
    {
        var (value, unit) = FormatSpeedParts(mbps);
        return $"{value} {unit}";
    }

    /// <summary>把速率拆分为数值与单位，便于在界面中用不同的字号排版。</summary>
    public static (string Value, string Unit) FormatSpeedParts(double mbps)
    {
        if (mbps >= 1000)
            return ($"{mbps / 1000:0.##}", "Gbps");
        if (mbps >= 1)
            return ($"{mbps:0.##}", "Mbps");
        if (mbps >= 0.001)
            return ($"{mbps * 1000:0.##}", "Kbps");
        return ("0", "bps");
    }
}
