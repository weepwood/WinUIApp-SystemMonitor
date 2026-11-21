using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using WinUIApp.Models;
using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>
/// Serializes sampling, gathers data on a worker, and publishes snapshots on the UI dispatcher.
/// </summary>
public sealed class MainViewModel : BaseViewModel, IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private DispatcherQueueTimer? _timer;
    private bool _isSampling;
    private bool _disposed;
    private int _samplingGeneration;
    private DateTimeOffset? _lastPublished;
    public bool HasSample => _lastPublished.HasValue;
    private DateTime _lastSlowSampleUtc = DateTime.MinValue;
    private int _slowIntervalSeconds = 5;
    private int _intervalSeconds = 1;
    private readonly ProcessService _processService = new();
    private bool _isMonitoring;
    private string _statusText = "准备监测";
    private string _lastUpdatedText = "尚未更新";

    private bool _cpuAlertEnabled;
    private bool _memoryAlertEnabled;
    private double _cpuAlertThreshold = 90;
    private double _memoryAlertThreshold = 90;
    private bool _alertDismissed;
    private bool _isAlertOpen;
    private string _alertTitle = string.Empty;
    private string _alertMessage = string.Empty;

    public CpuViewModel Cpu { get; } = new();
    public MemoryViewModel Memory { get; } = new();
    public DiskViewModel Disk { get; } = new();
    public NetworkViewModel Network { get; } = new();
    public SystemInfoViewModel SystemInfo { get; } = new();
    public ChartOptions Charts { get; } = new();
    public ProcessesViewModel Processes { get; } = new();
    public PowerViewModel Power { get; } = new();

    /// <summary>由窗口提供：返回 true 时跳过本次采样（例如窗口已最小化）。</summary>
    public Func<bool>? ShouldSkipSampling { get; set; }

    public bool IsMonitoring
    {
        get => _isMonitoring;
        private set
        {
            if (SetProperty(ref _isMonitoring, value))
            {
                OnPropertyChanged(nameof(MonitoringActionText));
                OnPropertyChanged(nameof(MonitoringActionGlyph));
                OnPropertyChanged(nameof(MonitoringOpacity));
                OnPropertyChanged(nameof(PausedOpacity));
            }
        }
    }

    public string MonitoringActionText => IsMonitoring ? "暂停" : "恢复";
    public string MonitoringActionGlyph => IsMonitoring ? "\uE769" : "\uE768";
    public double MonitoringOpacity => IsMonitoring ? 1 : 0;
    public double PausedOpacity => IsMonitoring ? 0 : 1;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetProperty(ref _lastUpdatedText, value);
    }

    public int IntervalSeconds => _intervalSeconds;

    public string IntervalText => $"每 {_intervalSeconds} 秒";

    public string OverviewSubtitle => HasSample
        ? $"采样间隔 {_intervalSeconds} 秒 · 最近更新 {_lastPublished:HH:mm:ss}"
        : $"采样间隔 {_intervalSeconds} 秒 · 正在获取首次采样…";

    public bool IsAlertOpen
    {
        get => _isAlertOpen;
        set
        {
            if (SetProperty(ref _isAlertOpen, value) && !value)
                _alertDismissed = true;
        }
    }

    public string AlertTitle
    {
        get => _alertTitle;
        private set => SetProperty(ref _alertTitle, value);
    }

    public string AlertMessage
    {
        get => _alertMessage;
        private set => SetProperty(ref _alertMessage, value);
    }

    public MainViewModel(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public void StartMonitoring()
    {
        if (_disposed) return;

        _timer ??= _dispatcherQueue.CreateTimer();
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _timer.Tick += OnTimerTick;
        _timer.Interval = TimeSpan.FromSeconds(_intervalSeconds);
        _timer.Start();

        IsMonitoring = true;
        StatusText = $"监测中 · 每 {_intervalSeconds} 秒";
        _ = RefreshAsync();
    }

    public void PauseMonitoring()
    {
        _timer?.Stop();
        _samplingGeneration++;
        IsMonitoring = false;
        StatusText = "已暂停";
        OnPropertyChanged(nameof(OverviewSubtitle));
    }

    public void ResumeMonitoring()
    {
        if (_disposed) return;
        if (_timer == null)
        {
            StartMonitoring();
            return;
        }

        _timer.Start();
        IsMonitoring = true;
        StatusText = $"监测中 · 每 {_intervalSeconds} 秒";
        _ = RefreshAsync();
    }

    public void ToggleMonitoring()
    {
        if (IsMonitoring)
            PauseMonitoring();
        else
            ResumeMonitoring();
    }

    /// <summary>立即采样一次（暂停状态下也可用）。</summary>
    public void RefreshNow()
    {
        if (_disposed) return;
        _ = RefreshAsync(force: true);
    }

    public void SetRefreshInterval(int seconds)
    {
        if (seconds is not (1 or 2 or 5 or 10)) return;

        _intervalSeconds = seconds;
        if (_timer != null)
            _timer.Interval = TimeSpan.FromSeconds(seconds);

        OnPropertyChanged(nameof(IntervalSeconds));
        OnPropertyChanged(nameof(IntervalText));
        OnPropertyChanged(nameof(OverviewSubtitle));

        if (IsMonitoring)
            StatusText = $"监测中 · 每 {seconds} 秒";
    }

    /// <summary>应用偏好设置：刷新间隔、图表选项和提醒阈值。</summary>
    public void Configure(MonitorPreferences preferences)
    {
        SetRefreshInterval(preferences.IntervalSeconds);

        Charts.HistoryCapacity = preferences.HistoryCapacity;
        Charts.ShowChartGrid = preferences.ShowChartGrid;
        Charts.ShowChartArea = preferences.ShowChartArea;
        Charts.ShowThresholdLine = preferences.ShowThresholdLine;
        Charts.Threshold = preferences.CpuAlertEnabled ? preferences.CpuAlertThreshold : 80;
        Charts.ShowTooltip = preferences.ShowChartTooltip;

        Cpu.History.SetCapacity(preferences.HistoryCapacity);
        Memory.History.SetCapacity(preferences.HistoryCapacity);
        SystemInfo.TopProcessCount = preferences.TopProcessCount;

        _slowIntervalSeconds = Math.Clamp(preferences.ProcessRefreshSeconds, 2, 10);
        Processes.SetSlowInterval(_slowIntervalSeconds);
        Processes.SetSearchText(preferences.ProcessFilterText);
        if (preferences.ProcessSortMode is >= 0 and <= 3)
            Processes.SortMode = preferences.ProcessSortMode;

        _cpuAlertEnabled = preferences.CpuAlertEnabled;
        _memoryAlertEnabled = preferences.MemoryAlertEnabled;
        _cpuAlertThreshold = preferences.CpuAlertThreshold;
        _memoryAlertThreshold = preferences.MemoryAlertThreshold;
        _alertDismissed = false;

        if (!_cpuAlertEnabled && !_memoryAlertEnabled)
            CloseAlert();
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        _ = RefreshAsync();
    }

    private async Task RefreshAsync(bool force = false)
    {
        if (_disposed || _isSampling) return;
        if (!force && (!IsMonitoring || ShouldSkipSampling?.Invoke() == true)) return;
        _isSampling = true;
        int generation = _samplingGeneration;

        // CPU / memory / network are lightweight samples relative to disk and process enumeration.
        // Slow metrics are sampled at most once every five seconds.
        bool collectSlow = DateTime.UtcNow - _lastSlowSampleUtc >= TimeSpan.FromSeconds(_slowIntervalSeconds);
        int topProcessCount = SystemInfo.TopProcessCount;
        try
        {
            var snapshot = await Task.Run(() => new Snapshot(
                Cpu.Capture(),
                Memory.Capture(),
                Network.Capture(),
                PowerService.GetStatus(),
                collectSlow ? Disk.Capture() : null,
                collectSlow ? SystemInfo.Capture() : null,
                collectSlow ? _processService.Capture(topProcessCount) : null));

            if (!_dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (_disposed || generation != _samplingGeneration) return;
                    Cpu.Apply(snapshot.Cpu);
                    Memory.Apply(snapshot.Memory);
                    Network.Apply(snapshot.Network);
                    Power.Apply(snapshot.Power);
                    if (snapshot.Disks != null) Disk.Apply(snapshot.Disks);
                    if (snapshot.System != null) SystemInfo.ApplyStatic(snapshot.System);
                    if (snapshot.Processes != null)
                    {
                        SystemInfo.ApplyProcessSnapshot(snapshot.Processes);
                        Processes.Apply(snapshot.Processes);
                    }
                    SystemInfo.UpdateUptime();

                    if (collectSlow) _lastSlowSampleUtc = DateTime.UtcNow;
                    bool firstSample = !HasSample;
                    _lastPublished = DateTimeOffset.Now;
                    LastUpdatedText = $"更新于 {_lastPublished:HH:mm:ss}";
                    OnPropertyChanged(nameof(OverviewSubtitle));
                    if (firstSample) OnPropertyChanged(nameof(HasSample));
                    if (IsMonitoring)
                        StatusText = $"监测中 · 每 {_intervalSeconds} 秒";

                    EvaluateAlerts();
                }
                catch (Exception ex)
                {
                    StatusText = $"显示更新失败：{ex.Message}";
                }
                finally
                {
                    _isSampling = false;
                }
            }))
                _isSampling = false;
        }
        catch (Exception ex)
        {
            if (!_dispatcherQueue.TryEnqueue(() =>
            {
                if (!_disposed && generation == _samplingGeneration) StatusText = $"采集失败：{ex.Message}";
                _isSampling = false;
            }))
                _isSampling = false;
        }
    }

    private void EvaluateAlerts()
    {
        bool cpuHit = _cpuAlertEnabled && Cpu.UsagePercent >= _cpuAlertThreshold;
        bool memoryHit = _memoryAlertEnabled && Memory.UsagePercent >= _memoryAlertThreshold;

        if (!cpuHit && !memoryHit)
        {
            _alertDismissed = false;
            CloseAlert();
            return;
        }

        if (cpuHit && memoryHit)
        {
            AlertTitle = "系统资源占用偏高";
            AlertMessage = $"CPU {Cpu.UsageText}、内存 {Memory.UsageText}，均超过提醒阈值。";
        }
        else if (cpuHit)
        {
            AlertTitle = "CPU 占用偏高";
            AlertMessage = $"当前 {Cpu.UsageText}，高于提醒阈值 {_cpuAlertThreshold:0}%。";
        }
        else
        {
            AlertTitle = "内存占用偏高";
            AlertMessage = $"当前 {Memory.UsageText}，高于提醒阈值 {_memoryAlertThreshold:0}%。";
        }

        if (!_alertDismissed)
            IsAlertOpen = true;
    }

    private void CloseAlert()
    {
        if (!_isAlertOpen) return;
        _isAlertOpen = false;
        OnPropertyChanged(nameof(IsAlertOpen));
    }

    // Called on the UI thread: use displayed values without triggering hardware queries.
    public string CreateSummary()
    {
        if (!HasSample) return string.Empty;
        var text = new StringBuilder();
        text.AppendLine($"本机监测摘要 · {_lastPublished:yyyy-MM-dd HH:mm:ss zzz}");
        text.AppendLine($"系统：{SystemInfo.OsVersion}，运行时间 {SystemInfo.Uptime}，{SystemInfo.ProcessCount} 个进程");
        text.AppendLine($"显卡：{SystemInfo.GraphicsName}，驱动 {SystemInfo.GraphicsDriver}");
        text.AppendLine($"电源：{Power.Summary}");
        text.AppendLine($"CPU：{Cpu.Name}，使用率 {Cpu.UsageText}，{Cpu.ClockSpeedText}");
        text.AppendLine($"内存：已用 {Memory.UsedGB:0.0} / {Memory.TotalText}（{Memory.UsageText}）");
        foreach (var drive in Disk.Drives)
            text.AppendLine($"磁盘 {drive.DriveName}：{drive.SizeText}");
        foreach (var adapter in Network.Adapters)
            text.AppendLine($"网络 {adapter.AdapterName}：下载 {adapter.DownloadAmountText}，上传 {adapter.UploadAmountText}");
        return text.ToString().TrimEnd();
    }

    public void StopMonitoring()
    {
        _disposed = true;
        _timer?.Stop();
        if (_timer != null)
            _timer.Tick -= OnTimerTick;
        _timer = null;
        IsMonitoring = false;
    }

    public void Dispose()
    {
        StopMonitoring();
        GC.SuppressFinalize(this);
    }

    private sealed record Snapshot(
        CpuInfo Cpu,
        MemoryInfo Memory,
        List<NetworkInfo> Network,
        PowerStatus Power,
        List<DiskInfo>? Disks,
        WinUIApp.Models.SystemInfo? System,
        ProcessSnapshot? Processes);
}
