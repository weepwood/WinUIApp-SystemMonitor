using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinUIApp.Models;

namespace WinUIApp.Services;

/// <summary>
/// 进程采集服务。64 位进程下优先用一次 NtQuerySystemInformation 批量取得全部进程
/// （名称、工作集、线程、句柄、内核/用户态时间），避免为每个 PID 打开句柄；
/// 系统调用不可用或运行在 32 位时回退到 Process API。
/// CPU 占用率由两次采样之间的时间差计算。
/// </summary>
public sealed class ProcessService
{
    private const int SystemProcessInformation = 5;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);

    // SYSTEM_PROCESS_INFORMATION（x64 / ARM64）字段偏移；32 位进程不使用该路径。
    private const int OffsetThreadCount = 0x04;
    private const int OffsetCreateTime = 0x20;
    private const int OffsetUserTime = 0x28;
    private const int OffsetKernelTime = 0x30;
    private const int OffsetImageNameLength = 0x38;
    private const int OffsetImageNameBuffer = 0x40;
    private const int OffsetProcessId = 0x50;
    private const int OffsetHandleCount = 0x60;
    private const int OffsetWorkingSetSize = 0x90;

    private readonly Dictionary<long, TimeSpan> _nativeCpuTime = new();
    private readonly Dictionary<int, TimeSpan> _legacyCpuTime = new();
    private long _previousTimestamp;
    // The native SYSTEM_PROCESS_INFORMATION parser is disabled until its per-Windows-build
    // layout can be validated; the managed fallback avoids unsafe pointer reads at startup.
    private bool _nativeUnavailable = true;

    public ProcessSnapshot Capture(int topCount = 5)
    {
        long now = Stopwatch.GetTimestamp();
        double elapsedSeconds = _previousTimestamp == 0
            ? 0
            : (now - _previousTimestamp) / (double)Stopwatch.Frequency;

        List<ProcessInfo>? rows = null;
        if (!_nativeUnavailable && Environment.Is64BitProcess)
            rows = CaptureNative(elapsedSeconds);

        if (rows is null || rows.Count == 0)
            rows = CaptureWithProcessApi(elapsedSeconds);
        else
            _legacyCpuTime.Clear();

        long threadTotal = 0;
        long handleTotal = 0;
        long workingSetTotal = 0;
        foreach (var process in rows)
        {
            threadTotal += process.ThreadCount;
            handleTotal += process.HandleCount;
            workingSetTotal += process.WorkingSetBytes;
        }

        _previousTimestamp = now;
        return new ProcessSnapshot(rows, (int)threadTotal, handleTotal, workingSetTotal, topCount);
    }

    /// <summary>一次系统调用枚举全部进程。</summary>
    private List<ProcessInfo>? CaptureNative(double elapsedSeconds)
    {
        List<ProcessInfo> rows = new(256);
        var currentCpuTime = new Dictionary<long, TimeSpan>(256);

        int bufferSize = 1 << 20;
        IntPtr buffer = IntPtr.Zero;

        try
        {
            int status;
            int required;
            while (true)
            {
                buffer = Marshal.AllocHGlobal(bufferSize);
                status = NtQuerySystemInformation(SystemProcessInformation, buffer, bufferSize, out required);

                if (status == 0)
                    break;

                Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;

                if (status != StatusInfoLengthMismatch)
                {
                    _nativeUnavailable = true;
                    return null;
                }

                bufferSize = Math.Max(bufferSize * 2, required);
                if (bufferSize > 256 << 20)
                {
                    _nativeUnavailable = true;
                    return null;
                }
            }

            int offset = 0;
            for (int guard = 0; guard < 100_000; guard++)
            {
                IntPtr entry = IntPtr.Add(buffer, offset);

                uint nextOffset = (uint)Marshal.ReadInt32(entry);
                int threadCount = (int)Marshal.ReadInt32(entry, OffsetThreadCount);
                long createTime = Marshal.ReadInt64(entry, OffsetCreateTime);
                long userTime = Marshal.ReadInt64(entry, OffsetUserTime);
                long kernelTime = Marshal.ReadInt64(entry, OffsetKernelTime);
                int nameLength = (ushort)Marshal.ReadInt16(entry, OffsetImageNameLength);
                IntPtr nameBuffer = Marshal.ReadIntPtr(entry, OffsetImageNameBuffer);
                int processId = (int)Marshal.ReadIntPtr(entry, OffsetProcessId).ToInt64();
                int handleCount = (int)Marshal.ReadInt32(entry, OffsetHandleCount);
                long workingSet = Marshal.ReadInt64(entry, OffsetWorkingSetSize);

                // 结构偏移一旦不匹配会读出垃圾值，做一次范围兜底。
                if (workingSet is < 0 or > 1L << 50)
                    workingSet = 0;
                if (threadCount is < 0 or > 1_000_000)
                    threadCount = 0;
                if (handleCount is < 0 or > 1_000_000)
                    handleCount = 0;

                string name = nameLength > 0 && nameBuffer != IntPtr.Zero
                    ? Marshal.PtrToStringUni(nameBuffer, nameLength / 2) ?? string.Empty
                    : ResolveName(processId);

                var cpuTime = TimeSpan.FromTicks(kernelTime + userTime);
                currentCpuTime[createTime] = cpuTime;

                double cpuPercent = 0;
                if (elapsedSeconds > 0.05 && _nativeCpuTime.TryGetValue(createTime, out var previous))
                {
                    double cpuSeconds = (cpuTime - previous).TotalSeconds;
                    if (cpuSeconds >= 0)
                    {
                        cpuPercent = Math.Clamp(
                            cpuSeconds / (elapsedSeconds * Environment.ProcessorCount) * 100.0, 0, 100);
                    }
                }

                if (!string.IsNullOrEmpty(name))
                {
                    rows.Add(new ProcessInfo
                    {
                        Id = processId,
                        Name = name,
                        WorkingSetBytes = Math.Max(0, workingSet),
                        CpuPercent = cpuPercent,
                        ThreadCount = threadCount,
                        HandleCount = handleCount,
                    });
                }

                if (nextOffset == 0)
                    break;

                offset += (int)nextOffset;
            }

            _nativeCpuTime.Clear();
            foreach (var pair in currentCpuTime)
                _nativeCpuTime[pair.Key] = pair.Value;

            // 正常系统至少有几十个进程；结果过少说明结构解析异常，永久回退。
            if (rows.Count < 2)
            {
                _nativeUnavailable = true;
                return null;
            }

            return rows;
        }
        catch (Exception)
        {
            _nativeUnavailable = true;
            return null;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>回退路径：逐个打开进程句柄读取属性。</summary>
    private List<ProcessInfo> CaptureWithProcessApi(double elapsedSeconds)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception)
        {
            return new List<ProcessInfo>();
        }

        var rows = new List<ProcessInfo>(processes.Length);
        var currentCpuTime = new Dictionary<int, TimeSpan>(processes.Length);

        foreach (var process in processes)
        {
            using (process)
            {
                int id;
                try { id = process.Id; } catch (Exception) { continue; }

                string name;
                try { name = process.ProcessName; } catch (Exception) { continue; }

                long workingSet = 0;
                try { workingSet = process.WorkingSet64; } catch (Exception) { }

                int threads = 0;
                try { threads = process.Threads.Count; } catch (Exception) { }

                int handles = 0;
                try { handles = process.HandleCount; } catch (Exception) { }

                double cpuPercent = 0;
                try
                {
                    var cpuTime = process.TotalProcessorTime;
                    currentCpuTime[id] = cpuTime;

                    if (elapsedSeconds > 0.05 && _legacyCpuTime.TryGetValue(id, out var previous))
                    {
                        double cpuSeconds = (cpuTime - previous).TotalSeconds;
                        if (cpuSeconds >= 0)
                        {
                            cpuPercent = Math.Clamp(
                                cpuSeconds / (elapsedSeconds * Environment.ProcessorCount) * 100.0, 0, 100);
                        }
                    }
                }
                catch (Exception) { }

                rows.Add(new ProcessInfo
                {
                    Id = id,
                    Name = name,
                    WorkingSetBytes = workingSet,
                    CpuPercent = cpuPercent,
                    ThreadCount = threads,
                    HandleCount = handles,
                });
            }
        }

        // 用本次结果替换基线，避免 PID 复用造成错误增量。
        _legacyCpuTime.Clear();
        foreach (var pair in currentCpuTime)
            _legacyCpuTime[pair.Key] = pair.Value;

        return rows;
    }

    /// <summary>
    /// 内核结构与 System Idle/System 等进程没有镜像名，退回 Process API 取名字。
    /// </summary>
    private static string ResolveName(int processId)
    {
        if (processId == 0)
            return "系统空闲进程";

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception)
        {
            return processId == 4 ? "系统" : string.Empty;
        }
    }

    /// <summary>
    /// 读取单个进程的路径、启动时间与优先级。访问受限或进程已退出时返回说明文字。
    /// 该调用可能较慢，应由后台线程执行。
    /// </summary>
    public static (string Path, string StartTime, string Priority) GetDetails(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);

            string path = "不可访问";
            try
            {
                string? fileName = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(fileName))
                    path = fileName;
            }
            catch (Exception) { }

            string startTime = "—";
            try { startTime = process.StartTime.ToString("yyyy-MM-dd HH:mm:ss"); } catch (Exception) { }

            string priority = "—";
            try { priority = DescribePriority(process.PriorityClass); } catch (Exception) { }

            return (path, startTime, priority);
        }
        catch (ArgumentException)
        {
            return ("进程已退出", "—", "—");
        }
        catch (Exception)
        {
            return ("不可访问", "—", "—");
        }
    }

    private static string DescribePriority(ProcessPriorityClass priority) => priority switch
    {
        ProcessPriorityClass.Idle => "空闲",
        ProcessPriorityClass.BelowNormal => "低于普通",
        ProcessPriorityClass.Normal => "普通",
        ProcessPriorityClass.AboveNormal => "高于普通",
        ProcessPriorityClass.High => "高",
        ProcessPriorityClass.RealTime => "实时",
        _ => priority.ToString(),
    };

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int infoClass, IntPtr info, int length, out int returnLength);
}
