using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.UI.Dispatching;
using WinUIApp.Models;

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
    private int _intervalSeconds = 1;
    private bool _isMonitoring;
    private string _statusText = "准备监测";
    private string _lastUpdatedText = "尚未更新";

    public CpuViewModel Cpu { get; } = new();
    public MemoryViewModel Memory { get; } = new();
    public DiskViewModel Disk { get; } = new();
    public NetworkViewModel Network { get; } = new();
    public SystemInfoViewModel SystemInfo { get; } = new();

    public bool IsMonitoring
    {
        get => _isMonitoring;
        private set => SetProperty(ref _isMonitoring, value);
    }

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

    public void SetRefreshInterval(int seconds)
    {
        if (seconds is not (1 or 2 or 5)) return;

        _intervalSeconds = seconds;
        if (_timer != null)
            _timer.Interval = TimeSpan.FromSeconds(seconds);

        if (IsMonitoring)
            StatusText = $"监测中 · 每 {seconds} 秒";
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_disposed || !IsMonitoring || _isSampling) return;
        _isSampling = true;
        int generation = _samplingGeneration;

        // CPU / memory / network are lightweight samples relative to disk and process enumeration.
        // Slow metrics are sampled at most once every five seconds.
        bool collectSlow = DateTime.UtcNow - _lastSlowSampleUtc >= TimeSpan.FromSeconds(5);
        try
        {
            var snapshot = await Task.Run(() => new Snapshot(
                Cpu.Capture(),
                Memory.Capture(),
                Network.Capture(),
                collectSlow ? Disk.Capture() : null,
                collectSlow ? SystemInfo.Capture() : null));

            if (!_dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (_disposed || generation != _samplingGeneration) return;
                    Cpu.Apply(snapshot.Cpu);
                    Memory.Apply(snapshot.Memory);
                    Network.Apply(snapshot.Network);
                    if (snapshot.Disks != null) Disk.Apply(snapshot.Disks);
                    if (snapshot.System != null) SystemInfo.Apply(snapshot.System);
                    SystemInfo.UpdateUptime();

                    if (collectSlow) _lastSlowSampleUtc = DateTime.UtcNow;
                    bool firstSample = !HasSample;
                    _lastPublished = DateTimeOffset.Now;
                    LastUpdatedText = $"更新于 {_lastPublished:HH:mm:ss}";
                    if (firstSample) OnPropertyChanged(nameof(HasSample));
                    if (IsMonitoring)
                        StatusText = $"监测中 · 每 {_intervalSeconds} 秒";
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

    // Called on the UI thread: use displayed values without triggering hardware queries.
    public string CreateSummary()
    {
        if (!HasSample) return string.Empty;
        var text = new StringBuilder();
        text.AppendLine($"本机监测摘要 · {_lastPublished:yyyy-MM-dd HH:mm:ss zzz}");
        text.AppendLine($"系统：{SystemInfo.OsVersion}，运行时间 {SystemInfo.Uptime}，{SystemInfo.ProcessCount} 个进程");
        text.AppendLine($"CPU：{Cpu.Name}，使用率 {Cpu.UsageText}，{Cpu.ClockSpeedText}");
        text.AppendLine($"内存：已用 {Memory.UsedGB:0.0} / {Memory.TotalText}（{Memory.UsageText}）");
        foreach (var drive in Disk.Drives)
            text.AppendLine($"磁盘 {drive.DriveName}：{drive.SizeText}");
        foreach (var adapter in Network.Adapters)
            text.AppendLine($"网络 {adapter.AdapterName}：下载 {adapter.DownloadSpeedText}，上传 {adapter.UploadSpeedText}");
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
        List<DiskInfo>? Disks,
        WinUIApp.Models.SystemInfo? System);
}
