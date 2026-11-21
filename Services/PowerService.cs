using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WinUIApp.Services;

public sealed record PowerStatus(
    bool HasBattery,
    bool IsAcOnline,
    int BatteryPercent,
    bool IsCharging,
    string Summary);

/// <summary>
/// 通过 GetSystemPowerStatus 读取电源与电池状态，台式机无电池时返回说明文字。
/// </summary>
public static class PowerService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    private static readonly PowerStatus Unknown = new(false, false, -1, false, "电源状态未知");

    public static PowerStatus GetStatus()
    {
        try
        {
            if (!GetSystemPowerStatus(out var status))
                return Unknown;

            bool acOnline = status.AcLineStatus == 1;
            int percent = status.BatteryLifePercent == 255 ? -1 : status.BatteryLifePercent;

            // 255 表示电池状态未知；128 表示设备没有系统电池。
            if (status.BatteryFlag == 255)
                return new PowerStatus(false, acOnline, -1, false, acOnline ? "交流电供电" : "电源状态未知");

            bool hasBattery = (status.BatteryFlag & 128) == 0;
            bool charging = hasBattery && (status.BatteryFlag & 8) != 0;

            if (!hasBattery)
                return new PowerStatus(false, acOnline, -1, false, "交流电供电 · 无电池");

            string summary;
            if (acOnline)
            {
                string level = percent >= 0 ? $"电池 {percent}%" : "电池";
                summary = charging ? $"交流电供电 · {level}（充电中）" : $"交流电供电 · {level}";
            }
            else
            {
                var parts = new List<string> { percent >= 0 ? $"电池供电 {percent}%" : "电池供电" };
                if (status.BatteryLifeTime != uint.MaxValue && status.BatteryLifeTime > 0)
                    parts.Add($"剩余 {FormatDuration(status.BatteryLifeTime)}");
                summary = string.Join(" · ", parts);
            }

            return new PowerStatus(true, acOnline, percent, charging, summary);
        }
        catch (Exception)
        {
            return Unknown;
        }
    }

    private static string FormatDuration(uint seconds)
    {
        TimeSpan span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours} 小时 {span.Minutes} 分";
        return $"{Math.Max(1, span.Minutes)} 分钟";
    }
}
