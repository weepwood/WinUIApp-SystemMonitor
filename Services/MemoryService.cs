using System;
using System.Linq;
using WinUIApp.Models;

namespace WinUIApp.Services;

/// <summary>
/// 内存使用量采集服务（使用 WMI）
/// </summary>
public class MemoryService
{
    public MemoryInfo GetMemoryInfo()
    {
        var info = new MemoryInfo();

        try
        {
            // Win32_OperatingSystem 获取物理内存总量和可用量
            var osResults = HardwareInfoService.ExecuteQuery(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            if (osResults.Count > 0)
            {
                var os = osResults[0];
                ulong totalKB = HardwareInfoService.GetPropertyValue<ulong>(os, "TotalVisibleMemorySize") ?? 0;
                ulong freeKB = HardwareInfoService.GetPropertyValue<ulong>(os, "FreePhysicalMemory") ?? 0;

                info.TotalGB = HardwareInfoService.BytesToGB(totalKB * 1024);
                info.AvailableGB = HardwareInfoService.BytesToGB(freeKB * 1024);
                info.UsedGB = info.TotalGB - info.AvailableGB;
                info.UsagePercent = info.TotalGB > 0 ? (float)Math.Round(info.UsedGB / info.TotalGB * 100, 1) : 0;
            }

            // Win32_PerfFormattedData_PerfOS_Memory 获取详细内存指标
            var memResults = HardwareInfoService.ExecuteQuery(
                "SELECT * FROM Win32_PerfFormattedData_PerfOS_Memory");
            if (memResults.Count > 0)
            {
                var mem = memResults[0];
                ulong committedBytes = HardwareInfoService.GetPropertyValue<ulong>(mem, "CommittedBytes") ?? 0;
                ulong cacheBytes = HardwareInfoService.GetPropertyValue<ulong>(mem, "CacheBytes") ?? 0;
                ulong poolPagedBytes = HardwareInfoService.GetPropertyValue<ulong>(mem, "PoolPagedBytes") ?? 0;
                ulong poolNonPagedBytes = HardwareInfoService.GetPropertyValue<ulong>(mem, "PoolNonpagedBytes") ?? 0;

                info.CommittedGB = HardwareInfoService.BytesToGB(committedBytes);
                info.CachedGB = HardwareInfoService.BytesToGB(cacheBytes);
                info.PagedPoolGB = HardwareInfoService.BytesToGB(poolPagedBytes);
                info.NonPagedPoolGB = HardwareInfoService.BytesToGB(poolNonPagedBytes);
            }
        }
        catch (Exception)
        {
            // 静默处理
        }

        return info;
    }
}
