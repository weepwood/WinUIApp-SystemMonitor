using System.Collections.Generic;

namespace WinUIApp.Models;

/// <summary>
/// CPU 信息和实时使用率数据
/// </summary>
public class CpuInfo
{
    public string Name { get; set; } = string.Empty;
    public float UsagePercent { get; set; }
    public float ClockSpeedMHz { get; set; }
    public int CoreCount { get; set; }
    public int LogicalProcessorCount { get; set; }
    public List<float> PerCoreUsage { get; set; } = new();
}
