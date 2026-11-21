using System;
using System.Collections.Generic;
using System.Diagnostics;
using WinUIApp.Models;

namespace WinUIApp.Services;

/// <summary>
/// 一次进程枚举同时得到总量、完整列表与内存排行，并用两次采样之间的
/// CPU 时间差计算进程占用率。服务保存上一次的 CPU 时间基线，因此应在
/// 同一个实例上重复调用 Capture。
/// </summary>
public sealed class ProcessService
{
    private readonly Dictionary<int, TimeSpan> _previousCpuTime = new();
    private long _previousTimestamp;

    public ProcessSnapshot Capture(int topCount = 5)
    {
        long now = Stopwatch.GetTimestamp();
        double elapsedSeconds = _previousTimestamp == 0
            ? 0
            : (now - _previousTimestamp) / (double)Stopwatch.Frequency;

        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception)
        {
            return ProcessSnapshot.Empty;
        }

        var rows = new List<ProcessInfo>(processes.Length);
        var currentCpuTime = new Dictionary<int, TimeSpan>(processes.Length);
        int threadTotal = 0;
        long handleTotal = 0;
        long workingSetTotal = 0;

        foreach (var process in processes)
        {
            using (process)
            {
                int id;
                try { id = process.Id; } catch (Exception) { continue; }

                string name = string.Empty;
                try { name = process.ProcessName; } catch (Exception) { }

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

                    if (elapsedSeconds > 0.05 && _previousCpuTime.TryGetValue(id, out var previous))
                    {
                        double cpuSeconds = (cpuTime - previous).TotalSeconds;
                        if (cpuSeconds >= 0)
                        {
                            cpuPercent = Math.Clamp(
                                cpuSeconds / (elapsedSeconds * Environment.ProcessorCount) * 100.0,
                                0,
                                100);
                        }
                    }
                }
                catch (Exception) { }

                threadTotal += threads;
                handleTotal += handles;
                workingSetTotal += workingSet;

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
        _previousCpuTime.Clear();
        foreach (var pair in currentCpuTime)
            _previousCpuTime[pair.Key] = pair.Value;
        _previousTimestamp = now;

        return new ProcessSnapshot(rows, threadTotal, handleTotal, workingSetTotal, topCount);
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
}
