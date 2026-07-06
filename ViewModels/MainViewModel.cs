using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace WinUIApp.ViewModels;

/// <summary>
/// 主 ViewModel，管理刷新定时器并协调各子 ViewModel 的数据更新
/// </summary>
public class MainViewModel : BaseViewModel, IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private DispatcherQueueTimer? _timer;

    public CpuViewModel Cpu { get; } = new();
    public MemoryViewModel Memory { get; } = new();
    public DiskViewModel Disk { get; } = new();
    public NetworkViewModel Network { get; } = new();
    public SystemInfoViewModel SystemInfo { get; } = new();

    public MainViewModel(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public void StartMonitoring()
    {
        // 首次立即采集静态信息
        SystemInfo.Refresh();

        // 设置 1 秒刷新定时器
        _timer = _dispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private async void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        // 在后台线程采集数据，避免阻塞 UI
        await Task.Run(() =>
        {
            Cpu.Refresh();
            Memory.Refresh();
            Disk.Refresh();
            Network.Refresh();
            SystemInfo.Refresh();
        });
    }

    public void StopMonitoring()
    {
        _timer?.Stop();
        _timer = null;
    }

    public void Dispose()
    {
        StopMonitoring();
        GC.SuppressFinalize(this);
    }
}
