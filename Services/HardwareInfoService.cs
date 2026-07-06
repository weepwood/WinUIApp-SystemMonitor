using System;
using System.Collections.Generic;
using System.Management;

namespace WinUIApp.Services;

/// <summary>
/// WMI 查询和系统信息获取的通用辅助服务
/// </summary>
public static class HardwareInfoService
{
    private static readonly ManagementScope Scope = new(@"\\.\ROOT\CIMV2");

    /// <summary>
    /// 执行 WMI 查询并返回结果列表
    /// </summary>
    public static List<ManagementObject> ExecuteQuery(string query)
    {
        var results = new List<ManagementObject>();
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, new ObjectQuery(query));
            using var collection = searcher.Get();
            foreach (ManagementObject obj in collection)
            {
                results.Add(obj);
            }
        }
        catch (Exception)
        {
            // WMI 查询失败时静默处理，返回空列表
        }
        return results;
    }

    /// <summary>
    /// 安全获取 ManagementObject 的属性值
    /// </summary>
    public static T? GetPropertyValue<T>(ManagementObject obj, string propertyName) where T : struct
    {
        try
        {
            var value = obj[propertyName];
            if (value != null && value is T typedValue)
                return typedValue;
        }
        catch (Exception) { }
        return null;
    }

    /// <summary>
    /// 安全获取 ManagementObject 的字符串属性值
    /// </summary>
    public static string GetStringProperty(ManagementObject obj, string propertyName)
    {
        try
        {
            var value = obj[propertyName];
            return value?.ToString() ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 将字节数转换为 GB
    /// </summary>
    public static double BytesToGB(ulong bytes) => Math.Round(bytes / (1024.0 * 1024.0 * 1024.0), 2);

    /// <summary>
    /// 将字节数转换为 GB (long 版本)
    /// </summary>
    public static double BytesToGB(long bytes) => Math.Round(bytes / (1024.0 * 1024.0 * 1024.0), 2);

    /// <summary>
    /// 将字节数转换为 MB
    /// </summary>
    public static double BytesToMB(long bytes) => Math.Round(bytes / (1024.0 * 1024.0), 2);

    /// <summary>
    /// 将比特数转换为 Mbps
    /// </summary>
    public static double BitsToMbps(long bits) => Math.Round(bits / 1_000_000.0, 2);

    /// <summary>
    /// 格式化字节数为可读字符串
    /// </summary>
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

    /// <summary>
    /// 格式化比特率为可读字符串
    /// </summary>
    public static string FormatSpeed(double mbps)
    {
        if (mbps >= 1000)
            return $"{mbps / 1000:0.##} Gbps";
        if (mbps >= 1)
            return $"{mbps:0.##} Mbps";
        if (mbps >= 0.001)
            return $"{mbps * 1000:0.##} Kbps";
        return "0 bps";
    }
}
