using System;
using System.Runtime.InteropServices;
using WinUIApp.Models;

namespace WinUIApp.Services;

/// <summary>
/// 内存采集服务。优先使用 GlobalMemoryStatusEx + GetPerformanceInfo 两个系统调用，
/// 只在不可用时回退 WMI。
/// </summary>
public class MemoryService
{
    public MemoryInfo GetMemoryInfo()
    {
        return TryGetNative(out var info) ? info : GetMemoryInfoWmi();
    }

    private static bool TryGetNative(out MemoryInfo info)
    {
        info = new MemoryInfo();

        try
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
                return false;

            info.TotalGB = HardwareInfoService.BytesToGB(status.TotalPhys);
            info.AvailableGB = HardwareInfoService.BytesToGB(status.AvailPhys);
            info.UsedGB = Math.Max(0, info.TotalGB - info.AvailableGB);
            info.UsagePercent = info.TotalGB > 0
                ? (float)Math.Round(info.UsedGB / info.TotalGB * 100, 1)
                : 0;

            var performance = new PerformanceInformation
            {
                cb = Marshal.SizeOf<PerformanceInformation>(),
            };

            if (GetPerformanceInfo(ref performance, performance.cb))
            {
                long pageSize = performance.PageSize.ToInt64();
                info.CommittedGB = HardwareInfoService.BytesToGB(ToBytes(performance.CommitTotal, pageSize));
                info.CachedGB = HardwareInfoService.BytesToGB(ToBytes(performance.SystemCache, pageSize));
                info.PagedPoolGB = HardwareInfoService.BytesToGB(ToBytes(performance.KernelPaged, pageSize));
                info.NonPagedPoolGB = HardwareInfoService.BytesToGB(ToBytes(performance.KernelNonpaged, pageSize));
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static ulong ToBytes(IntPtr pages, long pageSize)
    {
        long value = pages.ToInt64();
        return value > 0 && pageSize > 0 ? (ulong)value * (ulong)pageSize : 0;
    }

    /// <summary>降级路径：WMI 查询。</summary>
    private static MemoryInfo GetMemoryInfoWmi()
    {
        var info = new MemoryInfo();

        try
        {
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
            // 静默处理：界面保留上一次的数据。
        }

        return info;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        public int cb;
        public IntPtr CommitTotal;
        public IntPtr CommitLimit;
        public IntPtr CommitPeak;
        public IntPtr PhysicalTotal;
        public IntPtr PhysicalAvailable;
        public IntPtr SystemCache;
        public IntPtr KernelTotal;
        public IntPtr KernelPaged;
        public IntPtr KernelNonpaged;
        public IntPtr PageSize;
        public int HandleCount;
        public int ProcessCount;
        public int ThreadCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(ref PerformanceInformation info, int size);
}
