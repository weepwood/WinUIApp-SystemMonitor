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
    private CpuInfo _info;

    public CpuViewModel()
    {
        _info = _service.GetStaticCpuInfo();
    }

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
    private readonly float[] _usageHistory = new float[60];
    private int _historyIndex;
    public float[] UsageHistory => _usageHistory;
    public int HistoryCount => _historyIndex < 60 ? _historyIndex : 60;

    public void Refresh()
    {
        _service.UpdateCpuUsage(_info);

        UsagePercent = (float)Math.Round(_info.UsagePercent, 1);
        UsageText = $"{UsagePercent:0.0}%";
        ClockSpeedMHz = _info.ClockSpeedMHz;
        ClockSpeedText = $"{(ClockSpeedMHz / 1000):0.00} GHz";

        // 更新历史
        if (_historyIndex < 60)
        {
            _usageHistory[_historyIndex] = UsagePercent;
            _historyIndex++;
            OnPropertyChanged(nameof(HistoryCount));
        }
        else
        {
            Array.Copy(_usageHistory, 1, _usageHistory, 0, 59);
            _usageHistory[59] = UsagePercent;
        }

        // 更新每核心使用率
        var coreUsages = _info.PerCoreUsage;
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
        set => SetProperty(ref _usagePercent, value);
    }

    public string UsageText => $"{UsagePercent:0.0}%";
}
