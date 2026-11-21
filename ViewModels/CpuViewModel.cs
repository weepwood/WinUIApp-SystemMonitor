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
    public string DisplayName => string.IsNullOrWhiteSpace(_info.Name) ? "正在读取处理器信息…" : _info.Name;
    public int CoreCount => _info.CoreCount;
    public int LogicalProcessorCount => _info.LogicalProcessorCount;
    public string CoreCountText => CoreCount.ToString();
    public string LogicalProcessorCountText => LogicalProcessorCount.ToString();
    public string HardwareSummary => CoreCount > 0
        ? $"{CoreCount} 物理核心 · {LogicalProcessorCount} 线程"
        : "正在读取处理器信息…";

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

    private string _usageText = "—";
    public string UsageText
    {
        get => _usageText;
        set => SetProperty(ref _usageText, value);
    }

    private string _usageValueText = "—";
    public string UsageValueText
    {
        get => _usageValueText;
        set => SetProperty(ref _usageValueText, value);
    }

    private string _clockSpeedText = "—";
    public string ClockSpeedText
    {
        get => _clockSpeedText;
        set => SetProperty(ref _clockSpeedText, value);
    }

    /// <summary>每核心使用率；只在核心数变化时重建集合，之后原地更新。</summary>
    public ObservableCollection<CoreUsageItem> PerCoreUsage { get; } = new();

    public UsageHistory History { get; } = new();

    private bool _staticPublished;

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
        // 静态信息只发布一次，避免每秒重复通知 7 个不会变化的属性。
        if (!_staticPublished && info.LogicalProcessorCount > 0)
        {
            _staticPublished = true;
            OnPropertiesChanged(
                nameof(Name),
                nameof(DisplayName),
                nameof(CoreCount),
                nameof(LogicalProcessorCount),
                nameof(CoreCountText),
                nameof(LogicalProcessorCountText),
                nameof(HardwareSummary));
        }

        UsagePercent = (float)Math.Round(info.UsagePercent, 1);
        UsageText = $"{UsagePercent:0.0}%";
        UsageValueText = $"{UsagePercent:0.0}";
        ClockSpeedMHz = info.ClockSpeedMHz;
        ClockSpeedText = ClockSpeedMHz > 0 ? $"{ClockSpeedMHz / 1000:0.00} GHz" : "—";

        History.Append(UsagePercent);

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
                        Label = $"核心 {i}",
                    });
                }
            }

            while (PerCoreUsage.Count > coreUsages.Count)
                PerCoreUsage.RemoveAt(PerCoreUsage.Count - 1);
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
