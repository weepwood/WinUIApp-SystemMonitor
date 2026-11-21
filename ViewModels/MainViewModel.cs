using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Windows.Foundation;
using WinUIApp.Models;
using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>
/// 串联采样与界面。快采样（CPU / 内存 / 网络 / 电源 / 自身开销）与
/// 慢采样（磁盘 / 静态系统信息 / 进程）使用两个独立计时器与采样槽，
/// 慢采集不再拖慢每秒一次的界面刷新。
/// </summary>
public sealed class MainViewModel : BaseViewModel, IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ProcessService _processService = new();
    private readonly SelfMetricsService _selfMetrics = new();

    private DispatcherQueueTimer? _fastTimer;
    private DispatcherQueueTimer? _slowTimer;
    private bool _isFastSampling;
    private bool _isSlowSampling;
    private bool _disposed;
    private int _samplingGeneration;

    private DateTimeOffset? _lastPublished;
    public bool HasSample => _lastPublished.HasValue;

    private int _slowIntervalSeconds = 5;
    private int _intervalSeconds = 1;
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

        _fastTimer = RestartTimer(_fastTimer, OnFastTick, TimeSpan.FromSeconds(_intervalSeconds));
        _slowTimer = RestartTimer(_slowTimer, OnSlowTick, TimeSpan.FromSeconds(_slowIntervalSeconds));

        IsMonitoring = true;
        StatusText = $"监测中 · 每 {_intervalSeconds} 秒";
        _ = FastRefreshAsync(force: true);
        _ = SlowRefreshAsync(force: true);
    }

    public void PauseMonitoring()
    {
        _fastTimer?.Stop();
        _slowTimer?.Stop();
        _samplingGeneration++;
        IsMonitoring = false;
        StatusText = "已暂停";
        OnPropertyChanged(nameof(OverviewSubtitle));
    }

    public void ResumeMonitoring()
    {
        if (_disposed) return;
        if (_fastTimer == null || _slowTimer == null)
        {
            StartMonitoring();
            return;
        }

        _fastTimer.Start();
        _slowTimer.Start();
        IsMonitoring = true;
        StatusText = $"监测中 · 每 {_intervalSeconds} 秒";
        _ = FastRefreshAsync(force: true);
        _ = SlowRefreshAsync(force: true);
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
        _ = FastRefreshAsync(force: true);
        _ = SlowRefreshAsync(force: true);
    }

    public void SetRefreshInterval(int seconds)
    {
        if (seconds is not (1 or 2 or 5 or 10)) return;

        _intervalSeconds = seconds;
        if (_fastTimer != null)
            _fastTimer.Interval = TimeSpan.FromSeconds(seconds);

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
        Processes.SortDescending = preferences.ProcessSortDescending;

        if (_slowTimer != null)
            _slowTimer.Interval = TimeSpan.FromSeconds(_slowIntervalSeconds);

        _cpuAlertEnabled = preferences.CpuAlertEnabled;
        _memoryAlertEnabled = preferences.MemoryAlertEnabled;
        _cpuAlertThreshold = preferences.CpuAlertThreshold;
        _memoryAlertThreshold = preferences.MemoryAlertThreshold;
        _alertDismissed = false;

        if (!_cpuAlertEnabled && !_memoryAlertEnabled)
            CloseAlert();
    }

    private DispatcherQueueTimer RestartTimer(
        DispatcherQueueTimer? timer,
        TypedEventHandler<DispatcherQueueTimer, object> handler,
        TimeSpan interval)
    {
        timer ??= _dispatcherQueue.CreateTimer();
        timer.Stop();
        timer.Tick -= handler;
        timer.Tick += handler;
        timer.Interval = interval;
        timer.Start();
        return timer;
    }

    private void OnFastTick(DispatcherQueueTimer sender, object args) => _ = FastRefreshAsync();

    private void OnSlowTick(DispatcherQueueTimer sender, object args) => _ = SlowRefreshAsync();

    /// <summary>轻量指标：每次采样间隔刷新一次。</summary>
    private async Task FastRefreshAsync(bool force = false)
    {
        if (_disposed || _isFastSampling) return;
        if (!force && (!IsMonitoring || ShouldSkipSampling?.Invoke() == true)) return;
        _isFastSampling = true;
        int generation = _samplingGeneration;

        try
        {
            var snapshot = await Task.Run(() =>
            {
                var cpu = Cpu.Capture();
                var memory = Memory.Capture();
                var network = Network.Capture();
                var power = PowerService.GetStatus();
                var self = _selfMetrics.Sample();
                return new FastSnapshot(cpu, memory, network, power, self.CpuPercent, self.WorkingSetBytes);
            });

            if (!_dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (_disposed || generation != _samplingGeneration) return;
                    Cpu.Apply(snapshot.Cpu);
                    Memory.Apply(snapshot.Memory);
                    Network.Apply(snapshot.Network);
                    Power.Apply(snapshot.Power);
                    SystemInfo.ApplySelfMetrics(snapshot.SelfCpuPercent, snapshot.SelfWorkingSetBytes);
                    SystemInfo.UpdateUptime();
                    Publish();
                }
                catch (Exception ex)
                {
                    StatusText = $"显示更新失败：{ex.Message}";
                }
                finally
                {
                    _isFastSampling = false;
                }
            }))
                _isFastSampling = false;
        }
        catch (Exception ex)
        {
            if (!_dispatcherQueue.TryEnqueue(() =>
            {
                if (!_disposed && generation == _samplingGeneration)
                    StatusText = $"采集失败：{ex.Message}";
                _isFastSampling = false;
            }))
                _isFastSampling = false;
        }
    }

    /// <summary>重量指标：磁盘、静态系统信息与进程列表，按慢采样间隔刷新。</summary>
    private async Task SlowRefreshAsync(bool force = false)
    {
        if (_disposed || _isSlowSampling) return;
        if (!force && (!IsMonitoring || ShouldSkipSampling?.Invoke() == true)) return;
        _isSlowSampling = true;
        int generation = _samplingGeneration;
        int topProcessCount = SystemInfo.TopProcessCount;

        try
        {
            var snapshot = await Task.Run(() => new SlowSnapshot(
                Disk.Capture(),
                SystemInfo.Capture(),
                _processService.Capture(topProcessCount)));

            if (!_dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (_disposed || generation != _samplingGeneration) return;
                    Disk.Apply(snapshot.Disks);
                    SystemInfo.ApplyStatic(snapshot.System);
                    SystemInfo.ApplyProcessSnapshot(snapshot.Processes);
                    Processes.Apply(snapshot.Processes);
                    Publish();
                }
                catch (Exception ex)
                {
                    StatusText = $"显示更新失败：{ex.Message}";
                }
                finally
                {
                    _isSlowSampling = false;
                }
            }))
                _isSlowSampling = false;
        }
        catch (Exception ex)
        {
            if (!_dispatcherQueue.TryEnqueue(() =>
            {
                if (!_disposed && generation == _samplingGeneration)
                    StatusText = $"采集失败：{ex.Message}";
                _isSlowSampling = false;
            }))
                _isSlowSampling = false;
        }
    }

    private void Publish()
    {
        bool firstSample = !HasSample;
        _lastPublished = DateTimeOffset.Now;
        LastUpdatedText = $"更新于 {_lastPublished:HH:mm:ss}";
        OnPropertyChanged(nameof(OverviewSubtitle));
        if (firstSample)
            OnPropertyChanged(nameof(HasSample));
        if (IsMonitoring)
            StatusText = $"监测中 · 每 {_intervalSeconds} 秒";

        EvaluateAlerts();
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
        text.AppendLine($"监测开销：{SystemInfo.SelfOverheadText}");
        return text.ToString().TrimEnd();
    }

    public void StopMonitoring()
    {
        _disposed = true;
        _fastTimer?.Stop();
        _slowTimer?.Stop();

        if (_fastTimer != null)
            _fastTimer.Tick -= OnFastTick;
        if (_slowTimer != null)
            _slowTimer.Tick -= OnSlowTick;

        _fastTimer = null;
        _slowTimer = null;
        IsMonitoring = false;
    }

    public void Dispose()
    {
        StopMonitoring();
        GC.SuppressFinalize(this);
    }

    private sealed record FastSnapshot(
        CpuInfo Cpu,
        MemoryInfo Memory,
        List<NetworkInfo> Network,
        PowerStatus Power,
        float SelfCpuPercent,
        long SelfWorkingSetBytes);

    private sealed record SlowSnapshot(
        List<DiskInfo> Disks,
        WinUIApp.Models.SystemInfo System,
        ProcessSnapshot Processes);
}
