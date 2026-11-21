using System;
using System.Collections.Generic;
using System.Linq;

namespace WinUIApp.Models;

/// <summary>单个进程的一次采样结果。</summary>
public sealed class ProcessInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public long WorkingSetBytes { get; init; }
    public double CpuPercent { get; init; }
    public int ThreadCount { get; init; }
    public int HandleCount { get; init; }

    public string SizeText => FormatBytes(WorkingSetBytes);

    public static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824L)
            return $"{bytes / 1_073_741_824.0:0.00} GB";
        return $"{Math.Max(0, bytes) / 1_048_576.0:0.0} MB";
    }
}

/// <summary>
/// 一次进程枚举的快照：总量、工作集合计与按内存排序的前 N 个进程。
/// 进程可能随时退出或拒绝访问，缺失的字段按 0 处理，不影响其余结果。
/// </summary>
public sealed class ProcessSnapshot
{
    public static ProcessSnapshot Empty { get; } = new(Array.Empty<ProcessInfo>(), 0, 0, 0, 0);

    public ProcessSnapshot(
        IReadOnlyList<ProcessInfo> processes,
        int threadCount,
        long handleCount,
        long totalWorkingSetBytes,
        int topCount)
    {
        Processes = processes;
        ThreadCount = threadCount;
        HandleCount = handleCount;
        TotalWorkingSetBytes = totalWorkingSetBytes;

        int take = Math.Clamp(topCount, 0, 50);
        TopByMemory = take == 0
            ? Array.Empty<ProcessInfo>()
            : processes
                .OrderByDescending(p => p.WorkingSetBytes)
                .ThenBy(p => p.Id)
                .Take(take)
                .ToArray();
    }

    public IReadOnlyList<ProcessInfo> Processes { get; }
    public IReadOnlyList<ProcessInfo> TopByMemory { get; }
    public int ProcessCount => Processes.Count;
    public int ThreadCount { get; }
    public long HandleCount { get; }
    public long TotalWorkingSetBytes { get; }
}
