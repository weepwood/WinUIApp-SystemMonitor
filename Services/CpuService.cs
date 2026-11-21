using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WinUIApp.Models;

namespace WinUIApp.Services;

/// <summary>
/// CPU 采集服务。动态指标优先使用系统调用
/// （GetSystemTimes / NtQuerySystemInformation / CallNtPowerInformation），
/// 单次开销在微秒级；仅静态信息与降级路径使用 WMI。
/// </summary>
public class CpuService
{
    private const int SystemProcessorPerformanceInformation = 8;
    private const int ProcessorInformationLevel = 11;

    private bool _useWmiFallback;
    private ulong _previousIdle;
    private ulong _previousKernel;
    private ulong _previousUser;
    private bool _hasSystemTimesBaseline;

    private long[] _previousCoreIdle = Array.Empty<long>();
    private long[] _previousCoreKernel = Array.Empty<long>();
    private long[] _previousCoreUser = Array.Empty<long>();
    private float[] _coreUsage = Array.Empty<float>();
    private IntPtr _coreBuffer;
    private int _coreBufferSize;

    private IntPtr _powerBuffer;
    private int _powerBufferSize;

    private float _lastClockSpeedMHz;
    private DateTime _lastWmiClockUtc = DateTime.MinValue;

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
        _lastClockSpeedMHz = info.ClockSpeedMHz;
        return info;
    }

    /// <summary>更新 CPU 信息的动态部分。</summary>
    public void UpdateCpuUsage(CpuInfo info)
    {
        if (!_useWmiFallback && TryReadNativeUsage(info))
        {
            // 系统调用可用：频率与每核心均已更新。
        }
        else
        {
            UpdateCpuUsageWmi(info);
        }

        UpdateClockSpeed(info);
    }

    private bool TryReadNativeUsage(CpuInfo info)
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            _useWmiFallback = true;
            return false;
        }

        ulong idle = ToUInt64(idleTime);
        ulong kernel = ToUInt64(kernelTime);
        ulong user = ToUInt64(userTime);

        if (!_hasSystemTimesBaseline)
        {
            _previousIdle = idle;
            _previousKernel = kernel;
            _previousUser = user;
            _hasSystemTimesBaseline = true;
            info.UsagePercent = 0;
        }
        else
        {
            ulong idleDelta = idle - _previousIdle;
            ulong kernelDelta = kernel - _previousKernel;
            ulong userDelta = user - _previousUser;

            // kernel 时间包含 idle，因此总量 = kernel + user。
            ulong total = kernelDelta + userDelta;
            info.UsagePercent = total > 0
                ? (float)Math.Clamp((total - idleDelta) * 100.0 / total, 0, 100)
                : 0;

            _previousIdle = idle;
            _previousKernel = kernel;
            _previousUser = user;
        }

        TryReadPerCoreUsage(info);
        return true;
    }

    private void TryReadPerCoreUsage(CpuInfo info)
    {
        int count = Environment.ProcessorCount;
        int stride = Marshal.SizeOf<SystemProcessorPerformanceInformation>();

        if (_coreBuffer == IntPtr.Zero || _coreBufferSize < count * stride)
        {
            if (_coreBuffer != IntPtr.Zero)
                Marshal.FreeHGlobal(_coreBuffer);

            _coreBufferSize = count * stride;
            _coreBuffer = Marshal.AllocHGlobal(_coreBufferSize);
        }

        int status = NtQuerySystemInformation(
            SystemProcessorPerformanceInformation, _coreBuffer, _coreBufferSize, out _);

        if (status != 0)
            return;

        if (_previousCoreIdle.Length != count)
        {
            _previousCoreIdle = new long[count];
            _previousCoreKernel = new long[count];
            _previousCoreUser = new long[count];
            _coreUsage = new float[count];
        }

        int reported = 0;
        for (int i = 0; i < count; i++)
        {
            var core = Marshal.PtrToStructure<SystemProcessorPerformanceInformation>(
                IntPtr.Add(_coreBuffer, i * stride));

            long idle = core.IdleTime;
            long kernel = core.KernelTime;
            long user = core.UserTime;

            long idleDelta = idle - _previousCoreIdle[i];
            long totalDelta = (kernel - _previousCoreKernel[i]) + (user - _previousCoreUser[i]);

            _coreUsage[i] = totalDelta > 0
                ? (float)Math.Clamp((totalDelta - idleDelta) * 100.0 / totalDelta, 0, 100)
                : 0;

            _previousCoreIdle[i] = idle;
            _previousCoreKernel[i] = kernel;
            _previousCoreUser[i] = user;
            reported++;
        }

        if (reported > 0)
        {
            var usages = new List<float>(reported);
            for (int i = 0; i < reported; i++)
                usages.Add((float)Math.Round(_coreUsage[i], 1));
            info.PerCoreUsage = usages;
        }
    }

    /// <summary>优先用 CallNtPowerInformation 读取当前频率，失败时按 10 秒节流回退到 WMI。</summary>
    private void UpdateClockSpeed(CpuInfo info)
    {
        int count = Environment.ProcessorCount;
        int stride = Marshal.SizeOf<ProcessorPowerInformation>();
        int size = count * stride;

        if (_powerBuffer == IntPtr.Zero || _powerBufferSize < size)
        {
            if (_powerBuffer != IntPtr.Zero)
                Marshal.FreeHGlobal(_powerBuffer);

            _powerBufferSize = size;
            _powerBuffer = Marshal.AllocHGlobal(_powerBufferSize);
        }

        uint result = CallNtPowerInformation(
            ProcessorInformationLevel, IntPtr.Zero, 0, _powerBuffer, _powerBufferSize);

        if (result == 0)
        {
            uint maxCurrent = 0;
            uint maxLimit = 0;
            for (int i = 0; i < count; i++)
            {
                var power = Marshal.PtrToStructure<ProcessorPowerInformation>(
                    IntPtr.Add(_powerBuffer, i * stride));
                if (power.CurrentMhz > maxCurrent)
                    maxCurrent = power.CurrentMhz;
                if (power.MaxMhz > maxLimit)
                    maxLimit = power.MaxMhz;
            }

            uint report = maxCurrent > 0 ? maxCurrent : maxLimit;
            if (report > 0)
            {
                _lastClockSpeedMHz = report;
                info.ClockSpeedMHz = report;
                return;
            }
        }

        if ((DateTime.UtcNow - _lastWmiClockUtc).TotalSeconds < 10 && _lastClockSpeedMHz > 0)
        {
            info.ClockSpeedMHz = _lastClockSpeedMHz;
            return;
        }

        _lastWmiClockUtc = DateTime.UtcNow;
        float wmiClock = GetCurrentClockSpeedWmi();
        if (wmiClock > 0)
        {
            _lastClockSpeedMHz = wmiClock;
            info.ClockSpeedMHz = wmiClock;
        }
    }

    // ---- WMI 降级路径（系统调用不可用时） ----

    private void UpdateCpuUsageWmi(CpuInfo info)
    {
        info.UsagePercent = GetCpuUsageWmi();

        var coreUsages = GetPerCoreUsageWmi();
        if (coreUsages.Count > 0)
            info.PerCoreUsage = coreUsages;
    }

    private static float GetCpuUsageWmi()
    {
        try
        {
            var results = HardwareInfoService.ExecuteQuery("SELECT LoadPercentage FROM Win32_Processor");
            if (results.Count > 0)
            {
                var cpu = results[0];
                return HardwareInfoService.GetPropertyValue<ushort>(cpu, "LoadPercentage")
                    ?? HardwareInfoService.GetPropertyValue<uint>(cpu, "LoadPercentage")
                    ?? 0;
            }
        }
        catch (Exception) { }
        return 0f;
    }

    private static List<float> GetPerCoreUsageWmi()
    {
        var usages = new List<float>();
        try
        {
            var results = HardwareInfoService.ExecuteQuery(
                "SELECT Name, PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name <> '_Total' AND Name <> 'Idle'");

            var ordered = new List<(int Index, ulong Usage)>();
            foreach (var obj in results)
            {
                string name = HardwareInfoService.GetStringProperty(obj, "Name");
                if (!int.TryParse(name, out int index))
                    continue;

                ulong usage = HardwareInfoService.GetPropertyValue<ulong>(obj, "PercentProcessorTime") ?? 0;
                ordered.Add((index, Math.Min(usage, 100UL)));
            }

            ordered.Sort((a, b) => a.Index.CompareTo(b.Index));
            foreach (var entry in ordered)
                usages.Add(entry.Usage);
        }
        catch (Exception) { }
        return usages;
    }

    private static float GetCurrentClockSpeedWmi()
    {
        try
        {
            var results = HardwareInfoService.ExecuteQuery("SELECT CurrentClockSpeed FROM Win32_Processor");
            if (results.Count > 0)
                return HardwareInfoService.GetPropertyValue<uint>(results[0], "CurrentClockSpeed") ?? 0;
        }
        catch (Exception) { }
        return 0f;
    }

    // ---- P/Invoke ----

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemProcessorPerformanceInformation
    {
        public long IdleTime;
        public long KernelTime;
        public long UserTime;
        public long DpcTime;
        public long InterruptTime;
        public uint InterruptCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorPowerInformation
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int infoClass, IntPtr info, int length, out int returnLength);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint CallNtPowerInformation(
        int informationLevel, IntPtr inputBuffer, int inputBufferLength,
        IntPtr outputBuffer, int outputBufferLength);

    private static ulong ToUInt64(FileTime time) => ((ulong)time.High << 32) | time.Low;
}
