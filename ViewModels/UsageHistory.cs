using System;
using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>
/// 固定容量的采样窗口。横轴表示等间隔的采样点，而不是墙钟秒数。
/// 容量可以在运行时调整（30 / 60 / 120），调整时保留最近的采样。
/// </summary>
/// <remarks>
/// <para>
/// 采样先按真实时间写入 <see cref="TimeSeries"/>，再重采样成等间隔数组。
/// 因此把刷新间隔从 1 秒改到 10 秒、或暂停几分钟后恢复，曲线都不会被突然压缩或拉长；
/// 无论采样频率如何，横轴始终覆盖同样长度的真实时间。
/// </para>
/// <para>
/// 显示数组只在数据真正变化时重建：每秒一次的采样里数值常常与上一拍相同
/// （空闲时的 CPU、稳定的内存占用），此时不分配新数组、不触发绑定更新。
/// 平均值与峰值在重采样后一次性算出，避免两次全量遍历加两次 LINQ 迭代器分配。
/// </para>
/// </remarks>
public sealed class UsageHistory : BaseViewModel
{
    public const int DefaultCapacity = 60;
    public const int MinCapacity = 10;
    public const int MaxCapacity = 240;

    /// <summary>横轴每个采样点代表的时间（秒）。</summary>
    private const double StepSeconds = 1.0;

    /// <summary>真实采样缓冲：容量即最长窗口的秒数。</summary>
    private readonly TimeSeries _series = new(MaxCapacity);

    /// <summary>重采样缓冲，跨次复用，避免每次刷新都分配。</summary>
    private float[] _buffer = new float[MaxCapacity];

    private DateTime _latestUtc = DateTime.MinValue;
    private int _publishedValid = -1;
    private float _publishedLast = float.NaN;

    private int _capacity = DefaultCapacity;
    public int Capacity
    {
        get => _capacity;
        private set => SetProperty(ref _capacity, value);
    }

    private float[] _samples = Array.Empty<float>();
    public float[] Samples
    {
        get => _samples;
        private set => SetProperty(ref _samples, value);
    }

    private string _summary = "等待首次采样";
    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    private string _summaryShort = "等待首次采样";
    public string SummaryShort
    {
        get => _summaryShort;
        private set => SetProperty(ref _summaryShort, value);
    }

    private string _averageText = "平均 —";
    public string AverageText
    {
        get => _averageText;
        private set => SetProperty(ref _averageText, value);
    }

    private string _peakText = "峰值 —";
    public string PeakText
    {
        get => _peakText;
        private set => SetProperty(ref _peakText, value);
    }

    public bool HasSamples => _series.Count > 0;

    public void SetCapacity(int capacity)
    {
        capacity = Math.Clamp(capacity, MinCapacity, MaxCapacity);
        if (capacity == _capacity)
            return;

        Capacity = capacity;
        _publishedValid = -1;
        Publish(force: true);
    }

    public void Append(float value)
    {
        if (!float.IsFinite(value))
            value = 0f;

        value = Math.Clamp(value, 0f, 100f);

        _latestUtc = DateTime.UtcNow;
        _series.Append(ToSeconds(_latestUtc), value);
        Publish();
    }

    public void Clear()
    {
        _series.Clear();
        _latestUtc = DateTime.MinValue;
        _publishedValid = -1;
        Publish(force: true);
    }

    private void Publish(bool force = false)
    {
        double endSeconds = _latestUtc == DateTime.MinValue ? 0 : ToSeconds(_latestUtc);
        int points = _capacity;

        // 重采样：右对齐到最近一次采样时间，窗口外的旧点自然被裁掉。
        int valid = _series.Downsample(endSeconds, StepSeconds, points, _buffer);

        if (valid == 0)
        {
            _publishedValid = 0;
            _publishedLast = float.NaN;
            Samples = Array.Empty<float>();
            Summary = "等待首次采样";
            SummaryShort = "等待首次采样";
            AverageText = "平均 —";
            PeakText = "峰值 —";
            OnPropertyChanged(nameof(HasSamples));
            return;
        }

        // 数值与点数都没变化时不必重建数组（曲线像素完全相同）。
        float last = _buffer[valid - 1];
        if (!force && valid == _publishedValid && Math.Abs(last - _publishedLast) <= 0.001f)
            return;

        _publishedValid = valid;
        _publishedLast = last;

        // Downsample 右对齐写入，这里取末尾的有效区间。
        int offset = points - valid;
        var samples = new float[valid];
        Array.Copy(_buffer, offset, samples, 0, valid);
        Samples = samples;

        double sum = 0;
        float peak = 0;
        for (int i = 0; i < valid; i++)
        {
            float sample = samples[i];
            sum += sample;
            if (sample > peak)
                peak = sample;
        }

        double average = sum / valid;
        Summary = $"{valid} 点 · 平均 {average:0.0}% · 峰值 {peak:0.0}%";
        SummaryShort = $"平均 {average:0.0}% · 峰值 {peak:0.0}%";
        AverageText = $"平均 {average:0.0}%";
        PeakText = $"峰值 {peak:0.0}%";
        OnPropertyChanged(nameof(HasSamples));
    }

    private static double ToSeconds(DateTime utc) => utc.Ticks / (double)TimeSpan.TicksPerSecond;
}
