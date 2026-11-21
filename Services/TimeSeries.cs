using System;
using System.Collections.Generic;

namespace WinUIApp.Services;

/// <summary>
/// 按真实时间做窗口的内存环形缓冲（最近 N 秒）。
/// <para>
/// 采样间隔、暂停、切页都会让「每拍一个点」的时间轴漂移：
/// 暂停 5 分钟后曲线看起来像连续采样。这里记录时间戳，
/// 让窗口既能做时间轴对齐，也能在窗口内按固定步长降采样（见 <see cref="Downsample"/>），
/// 于是采样间隔从 1 秒换到 10 秒时不需要清空或重填历史。
/// </para>
/// <para>
/// 线程安全：后台采样线程写入，UI 线程读取快照。
/// </para>
/// </summary>
public sealed class TimeSeries
{
    private readonly double[] _timestamps;
    private readonly float[] _values;
    private int _start;
    private int _count;
    private readonly object _gate = new();

    public TimeSeries(int capacitySeconds)
    {
        Capacity = Math.Max(2, capacitySeconds);
        _timestamps = new double[Capacity];
        _values = new float[Capacity];
    }

    public int Capacity { get; }

    public int Count
    {
        get
        {
            lock (_gate)
                return _count;
        }
    }

    /// <summary>追加一个采样点并丢弃超出窗口的旧数据。</summary>
    public void Append(double timestampSeconds, float value)
    {
        if (!double.IsFinite(timestampSeconds))
            return;

        if (!float.IsFinite(value))
            value = 0f;

        value = Math.Clamp(value, 0f, 100f);

        lock (_gate)
        {
            double cutoff = timestampSeconds - Capacity;

            // 读取路径可能落后于写入（暂停后恢复），先丢弃过旧的点。
            while (_count > 0 && _timestamps[_start] < cutoff)
            {
                _start = (_start + 1) % Capacity;
                _count--;
            }

            if (_count == Capacity)
            {
                _start = (_start + 1) % Capacity;
                _count--;
            }

            int index = (_start + _count) % Capacity;
            _timestamps[index] = timestampSeconds;
            _values[index] = value;
            _count++;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _start = 0;
            _count = 0;
        }
    }

    /// <summary>按时间顺序拷贝出所有采样点（只读快照）。</summary>
    public void CopyTo(List<(double Timestamp, float Value)> destination)
    {
        destination.Clear();
        lock (_gate)
        {
            for (int i = 0; i < _count; i++)
            {
                int index = (_start + i) % Capacity;
                destination.Add((_timestamps[index], _values[index]));
            }
        }
    }

    /// <summary>
    /// 把真实采样重采样成等间隔数组，并对齐到窗口右端。
    /// </summary>
    /// <param name="endSeconds">窗口右端（通常是最近一次采样时间）。</param>
    /// <param name="stepSeconds">横轴期望的时间步长（通常 1 秒）。</param>
    /// <param name="points">横轴总点数，也是 buffer 的长度。</param>
    /// <param name="buffer">复用数组，避免每次刷新都分配。</param>
    /// <returns>
    /// 有效点数。数据不足时只填数组末尾（右对齐），左侧留空，
    /// 与「新数据从右侧进入」的观感一致。
    /// </returns>
    /// <remarks>
    /// 只保留最近 <paramref name="points"/> 个采样，并保证这些采样全部可见：
    /// 按 1 秒采样时步长恰好是 1 秒；把刷新间隔改成 10 秒或暂停几分钟后恢复时，
    /// 步长会自动放大到能容纳全部采样，曲线因此既不会丢点、也不会把空窗误画成旧数值。
    /// </remarks>
    public int Downsample(double endSeconds, double stepSeconds, int points, float[] buffer)
    {
        if (points <= 0 || buffer.Length < points || stepSeconds <= 0)
            return 0;

        lock (_gate)
        {
            if (_count == 0)
                return 0;

            int visible = Math.Min(_count, points);
            int offset = points - visible;

            // 本次窗口最左端的采样。
            int firstIndex = (_start + _count - visible) % Capacity;
            double firstSeconds = _timestamps[firstIndex];

            double span = endSeconds - firstSeconds;
            double step = span > 0 && visible > 1
                ? Math.Max(stepSeconds, span / (visible - 1))
                : stepSeconds;

            int cursorIndex = firstIndex;
            double cursorTime = firstSeconds;
            float cursorValue = _values[firstIndex];

            for (int i = 0; i < visible; i++)
            {
                double target = endSeconds - step * (visible - 1 - i);
                int steps = (int)Math.Floor((target - cursorTime) / step);

                // 游标单调前移，取不晚于目标时间的最近一个采样值。
                if (steps > 0)
                {
                    int advance = Math.Min(steps, visible - 1);
                    cursorIndex = (cursorIndex + advance) % Capacity;
                    cursorTime = _timestamps[cursorIndex];
                    cursorValue = _values[cursorIndex];
                }

                buffer[offset + i] = cursorValue;
            }

            return visible;
        }
    }
}
