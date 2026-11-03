using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using WinUIApp.Models;

namespace WinUIApp.Services;

/// <summary>
/// CPU 使用率、频率、核心信息采集服务（纯 WMI 实现，无 PerformanceCounter 依赖）
/// </summary>
public class CpuService
{
    public CpuInfo GetStaticCpuInfo()
    {
        var info = new CpuInfo();

        try
        {
            var results = HardwareInfoService.ExecuteQuery("SELECT * FROM Win32_Processor");
            if (results.Count > 0)
            {
                var cpu = results[0];
                info.Name = HardwareInfoService.GetStringProperty(cpu, "Name").Trim();
                info.CoreCount = (int)(HardwareInfoService.GetPropertyValue<uint>(cpu, "NumberOfCores") ?? (uint)Environment.ProcessorCount);
                info.LogicalProcessorCount = (int)(HardwareInfoService.GetPropertyValue<uint>(cpu, "NumberOfLogicalProcessors") ?? (uint)Environment.ProcessorCount);
                info.ClockSpeedMHz = HardwareInfoService.GetPropertyValue<uint>(cpu, "MaxClockSpeed") ?? 0u;
            }
        }
        catch (Exception)
        {
            info.Name = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";
            info.CoreCount = Environment.ProcessorCount;
            info.LogicalProcessorCount = Environment.ProcessorCount;
        }

        if (info.LogicalProcessorCount <= 0)
            info.LogicalProcessorCount = Environment.ProcessorCount;
        if (info.CoreCount <= 0)
            info.CoreCount = Environment.ProcessorCount;
        info.PerCoreUsage = new List<float>(new float[info.LogicalProcessorCount]);
        return info;
    }

    /// <summary>
    /// 获取 CPU 总体使用率 (使用 Win32_Processor.LoadPercentage)
    /// </summary>
    public float GetCpuUsage()
    {
        try
        {
            var results = HardwareInfoService.ExecuteQuery("SELECT LoadPercentage FROM Win32_Processor");
            if (results.Count > 0)
            {
                var cpu = results[0];
                // WMI Win32_Processor.LoadPercentage is CIM_UINT16 (ushort).
                return HardwareInfoService.GetPropertyValue<ushort>(cpu, "LoadPercentage")
                    ?? HardwareInfoService.GetPropertyValue<uint>(cpu, "LoadPercentage")
                    ?? 0;
            }
        }
        catch (Exception) { }
        return 0f;
    }

    /// <summary>
    /// 获取每核心 CPU 使用率 (使用 Win32_PerfFormattedData_PerfOS_Processor)
    /// </summary>
    public List<float> GetPerCoreUsage()
    {
        var usages = new List<float>();
        try
        {
            var results = HardwareInfoService.ExecuteQuery(
                "SELECT Name, PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name <> '_Total' AND Name <> 'Idle'");
            foreach (var obj in results.OrderBy(o =>
                int.TryParse(HardwareInfoService.GetStringProperty(o, "Name"), out int index)
                    ? index : int.MaxValue))
            {
                var usage = HardwareInfoService.GetPropertyValue<ulong>(obj, "PercentProcessorTime") ?? 0;
                usages.Add((float)Math.Clamp((double)usage, 0, 100));
            }
        }
        catch (Exception) { }
        return usages;
    }

    /// <summary>
    /// 获取当前 CPU 频率
    /// </summary>
    public float GetCurrentClockSpeed()
    {
        try
        {
            var results = HardwareInfoService.ExecuteQuery("SELECT CurrentClockSpeed FROM Win32_Processor");
            if (results.Count > 0)
            {
                return HardwareInfoService.GetPropertyValue<uint>(results[0], "CurrentClockSpeed") ?? 0;
            }
        }
        catch (Exception) { }
        return 0f;
    }

    /// <summary>
    /// 更新 CPU 信息的动态部分
    /// </summary>
    public void UpdateCpuUsage(CpuInfo info)
    {
        // 总体使用率
        info.UsagePercent = GetCpuUsage();

        // 每核心使用率
        var coreUsages = GetPerCoreUsage();
        if (coreUsages.Count > 0)
        {
            info.PerCoreUsage = coreUsages;
        }
        // 如果 PerformanceCounter 不可用，WMI 可能返回空，保持上一次的值

        // 当前频率
        var clockSpeed = GetCurrentClockSpeed();
        if (clockSpeed > 0)
            info.ClockSpeedMHz = clockSpeed;
    }
}
