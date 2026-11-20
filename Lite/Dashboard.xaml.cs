using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WinUIApp.Models;
using WinUIApp.Services;

namespace SystemMonitor.Lite;

public partial class Dashboard : Window
{
    private readonly CpuService _cpu = new();
    private readonly MemoryService _memory = new();
    private readonly DiskService _disk = new();
    private readonly NetworkService _network = new();
    private readonly Stopwatch _slowClock = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _paused, _busy, _closed;
    private int _generation;
    private string? _cpuName;
    private DateTime? _updated;

    public Dashboard()
    {
        InitializeComponent();
        Device.Text = $"{Environment.MachineName}   ·   Windows x64   ·   本机实时监测";
        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) => { _timer.Start(); await RefreshAsync(); };
        Closed += (_, _) => { _closed = true; _timer.Stop(); };
    }

    private async Task RefreshAsync()
    {
        if (_busy || _paused || _closed) return;
        _busy = true;
        int generation = _generation;
        bool slow = !_slowClock.IsRunning || _slowClock.Elapsed >= TimeSpan.FromSeconds(5);
        try
        {
            var sample = await Task.Run(() =>
            {
                _cpuName ??= _cpu.GetStaticCpuInfo().Name;
                var stats = new SystemInfo();
                if (slow) ProcessService.Sample(stats);
                return (Cpu: _cpu.GetCpuUsage(), Memory: _memory.GetMemoryInfo(), Network: _network.GetNetworkInfos(),
                        Disks: slow ? _disk.GetDiskInfos() : null, System: stats);
            });
            if (_closed || generation != _generation) return;
            CpuValue.Text = $"{sample.Cpu:0.#}%";
            CpuBar.Value = sample.Cpu;
            CpuDetail.Text = string.IsNullOrEmpty(_cpuName) ? $"{Environment.ProcessorCount} 个逻辑处理器" : _cpuName;
            RamValue.Text = sample.Memory.TotalGB > 0 ? $"{sample.Memory.UsagePercent:0.#}%" : "—";
            RamDetail.Text = sample.Memory.TotalGB > 0 ? $"可用 {sample.Memory.AvailableGB:0.0} / {sample.Memory.TotalGB:0.0} GiB" : "内存数据不可用";
            RamBar.Value = sample.Memory.UsagePercent;
            Adapters.ItemsSource = sample.Network.Where(n => n.IsActive).Select(n => new
            {
                n.Description, Download = HardwareInfoService.FormatSpeed(n.DownloadSpeedMbps),
                Upload = HardwareInfoService.FormatSpeed(n.UploadSpeedMbps)
            }).ToArray();
            if (sample.Disks != null)
            {
                Processes.ItemsSource = sample.System.TopProcesses;
                Drives.ItemsSource = sample.Disks.Select(d => new
                {
                    d.DriveName, Capacity = d.IsReady ? $"{d.FreeSpaceGB:0.0} / {d.TotalSizeGB:0.0} GiB" : "未就绪",
                    Throughput = d.IsReady ? $"{d.ReadSpeedMBps:0.0} / {d.WriteSpeedMBps:0.0}" : "—"
                }).ToArray();
                _slowClock.Restart();
            }
            _updated = DateTime.Now;
            Status.Text = $"监测中   ·   最近更新 {_updated:HH:mm:ss}   ·   进程与磁盘至少间隔 5 秒采样";
            Title = $"系统监测 · Lite   |   CPU {CpuValue.Text}   内存 {RamValue.Text}";
        }
        catch (Exception ex)
        {
            if (!_closed && generation == _generation) Status.Text = $"采样失败，将在下次刷新时重试：{ex.Message}";
        }
        finally { _busy = false; }
    }

    private async void TogglePause(object sender, RoutedEventArgs args)
    {
        _paused = !_paused;
        _generation++;
        Pause.Content = _paused ? "恢复采样" : "暂停采样";
        if (_paused)
        {
            _timer.Stop();
            Status.Text = _updated.HasValue ? $"已暂停   ·   最近更新 {_updated:HH:mm:ss}" : "已暂停   ·   尚无采样";
        }
        else
        {
            Status.Text = "正在恢复采样…";
            _timer.Start();
            await RefreshAsync();
        }
    }

    private void IntervalChanged(object sender, SelectionChangedEventArgs args)
    {
        if (sender is ComboBox { SelectedItem: ComboBoxItem item } && int.TryParse(item.Tag?.ToString(), out int seconds))
            _timer.Interval = TimeSpan.FromSeconds(seconds);
    }
}
