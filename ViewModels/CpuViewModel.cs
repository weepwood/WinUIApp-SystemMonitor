using System;
using System.Collections.ObjectModel;
using WinUIApp.Models;
using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>
/// CPU 使用率数据 ViewModel
/// </summary>
public class CpuViewModel : BaseViewModel
{
    private readonly CpuService _service = new();
    private CpuInfo _info = new();


    public string Name => _info.Name;
    public int CoreCount => _info.CoreCount;
    public int LogicalProcessorCount => _info.LogicalProcessorCount;

    private float _usagePercent;
    public float UsagePercent
    {
        get => _usagePercent;
        set => SetProperty(ref _usagePercent, value);
    }

    private float _clockSpeedMHz;
    public float ClockSpeedMHz
    {
        get => _clockSpeedMHz;
        set => SetProperty(ref _clockSpeedMHz, value);
    }

    private string _usageText = "0%";
    public string UsageText
    {
        get => _usageText;
        set => SetProperty(ref _usageText, value);
    }

    private string _clockSpeedText = "0 MHz";
    public string ClockSpeedText
    {
        get => _clockSpeedText;
        set => SetProperty(ref _clockSpeedText, value);
    }

    public ObservableCollection<CoreUsageItem> PerCoreUsage { get; } = new();

    // CPU 历史数据（最近 60 秒）
    private readonly System.Collections.Generic.Queue<float> _usageHistory = new(60);
    public float[] UsageHistory { get; private set; } = Array.Empty<float>();
    public int HistoryCount => UsageHistory.Length;

    // Hardware queries run on the sampling worker, never on the UI thread.
    public CpuInfo Capture()
    {
        if (_info.LogicalProcessorCount == 0)
            _info = _service.GetStaticCpuInfo();

        _service.UpdateCpuUsage(_info);
        return _info;
    }

    // Called only on the UI dispatcher.
    public void Apply(CpuInfo info)
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(CoreCount));
        OnPropertyChanged(nameof(LogicalProcessorCount));

        UsagePercent = (float)Math.Round(info.UsagePercent, 1);
        UsageText = $"{UsagePercent:0.0}%";
        ClockSpeedMHz = info.ClockSpeedMHz;
        ClockSpeedText = $"{(ClockSpeedMHz / 1000):0.00} GHz";

        // Allocate a fresh array so the chart's dependency property receives a new value.
        if (_usageHistory.Count == 60)
            _usageHistory.Dequeue();
        _usageHistory.Enqueue(UsagePercent);
        UsageHistory = _usageHistory.ToArray();
        OnPropertyChanged(nameof(UsageHistory));
        OnPropertyChanged(nameof(HistoryCount));

        // 更新每核心使用率
        var coreUsages = info.PerCoreUsage;
        if (coreUsages != null)
        {
            for (int i = 0; i < coreUsages.Count; i++)
            {
                var usage = (float)Math.Round(coreUsages[i], 1);
                if (i < PerCoreUsage.Count)
                {
                    PerCoreUsage[i].UsagePercent = usage;
                }
                else
                {
                    PerCoreUsage.Add(new CoreUsageItem
                    {
                        CoreIndex = i,
                        UsagePercent = usage,
                        Label = $"核心 {i}"
                    });
                }
            }
        }
    }
}

public class CoreUsageItem : BaseViewModel
{
    public int CoreIndex { get; set; }
    public string Label { get; set; } = string.Empty;

    private float _usagePercent;
    public float UsagePercent
    {
        get => _usagePercent;
        set
        {
            if (SetProperty(ref _usagePercent, value))
                OnPropertyChanged(nameof(UsageText));
        }
    }

    public string UsageText => $"{UsagePercent:0.0}%";
}
