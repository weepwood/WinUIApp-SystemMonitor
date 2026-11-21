using System;
using System.Collections.Generic;
using System.Linq;

namespace WinUIApp.ViewModels;

/// <summary>
/// 固定容量的采样窗口。横轴表示等间隔的采样点，而不是墙钟秒数。
/// 容量可以在运行时调整（30 / 60 / 120），调整时保留最近的采样。
/// </summary>
public sealed class UsageHistory : BaseViewModel
{
    public const int DefaultCapacity = 60;
    public const int MinCapacity = 10;
    public const int MaxCapacity = 240;

    private readonly List<float> _values = new(DefaultCapacity);

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

    public bool HasSamples => _values.Count > 0;

    public void SetCapacity(int capacity)
    {
        capacity = Math.Clamp(capacity, MinCapacity, MaxCapacity);
        if (capacity == _capacity)
            return;

        Capacity = capacity;
        if (_values.Count > capacity)
            _values.RemoveRange(0, _values.Count - capacity);

        Publish();
    }

    public void Append(float value)
    {
        if (!float.IsFinite(value))
            value = 0f;

        _values.Add(Math.Clamp(value, 0f, 100f));
        if (_values.Count > _capacity)
            _values.RemoveRange(0, _values.Count - _capacity);

        Publish();
    }

    public void Clear()
    {
        _values.Clear();
        Publish();
    }

    private void Publish()
    {
        Samples = _values.ToArray();
        if (_values.Count == 0)
        {
            Summary = "等待首次采样";
            SummaryShort = "等待首次采样";
            AverageText = "平均 —";
            PeakText = "峰值 —";
        }
        else
        {
            double average = _values.Average();
            double peak = _values.Max();
            Summary = $"{_values.Count} 点 · 平均 {average:0.0}% · 峰值 {peak:0.0}%";
            SummaryShort = $"平均 {average:0.0}% · 峰值 {peak:0.0}%";
            AverageText = $"平均 {average:0.0}%";
            PeakText = $"峰值 {peak:0.0}%";
        }

        OnPropertyChanged(nameof(HasSamples));
    }
}
