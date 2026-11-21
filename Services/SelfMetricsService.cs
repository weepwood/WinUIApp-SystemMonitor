using System;
using System.Diagnostics;

namespace WinUIApp.Services;

/// <summary>
/// 应用自身的开销：CPU 占用率（两次采样间的处理器时间差）与工作集。
/// 用与其它进程相同的口径计算，便于直观对比「监测进程本身的成本」。
/// </summary>
public sealed class SelfMetricsService
{
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan _previousCpuTime;
    private long _previousTimestamp;

    public (float CpuPercent, long WorkingSetBytes) Sample()
    {
        long now = Stopwatch.GetTimestamp();
        double elapsedSeconds = _previousTimestamp == 0
            ? 0
            : (now - _previousTimestamp) / (double)Stopwatch.Frequency;
        _previousTimestamp = now;

        TimeSpan cpuTime;
        long workingSet;
        try
        {
            _process.Refresh();
            cpuTime = _process.TotalProcessorTime;
            workingSet = _process.WorkingSet64;
        }
        catch (Exception)
        {
            return (0, 0);
        }

        float percent = 0;
        if (elapsedSeconds > 0.05)
        {
            double cpuSeconds = (cpuTime - _previousCpuTime).TotalSeconds;
            if (cpuSeconds >= 0)
            {
                percent = (float)Math.Clamp(
                    cpuSeconds / (elapsedSeconds * Environment.ProcessorCount) * 100.0, 0, 100);
            }
        }

        _previousCpuTime = cpuTime;
        return (percent, workingSet);
    }
}
