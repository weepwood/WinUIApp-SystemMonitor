using System;
using System.Diagnostics;
using System.Linq;
using WinUIApp.Models;
using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>
/// 系统信息 ViewModel（OS、BIOS、运行时间等）
/// </summary>
public class SystemInfoViewModel : BaseViewModel
{
    private SystemInfo _info = new();
    private readonly Stopwatch _uptimeStopwatch = Stopwatch.StartNew();

    private string _osVersion = string.Empty;
    public string OsVersion
    {
        get => _osVersion;
        set => SetProperty(ref _osVersion, value);
    }

    private string _osBuild = string.Empty;
    public string OsBuild
    {
        get => _osBuild;
        set => SetProperty(ref _osBuild, value);
    }

    private string _machineName = string.Empty;
    public string MachineName
    {
        get => _machineName;
        set => SetProperty(ref _machineName, value);
    }

    private string _uptime = string.Empty;
    public string Uptime
    {
        get => _uptime;
        set => SetProperty(ref _uptime, value);
    }

    private string _biosVersion = string.Empty;
    public string BiosVersion
    {
        get => _biosVersion;
        set => SetProperty(ref _biosVersion, value);
    }

    private string _biosManufacturer = string.Empty;
    public string BiosManufacturer
    {
        get => _biosManufacturer;
        set => SetProperty(ref _biosManufacturer, value);
    }

    private string _motherboardManufacturer = string.Empty;
    public string MotherboardManufacturer
    {
        get => _motherboardManufacturer;
        set => SetProperty(ref _motherboardManufacturer, value);
    }

    private string _motherboardProduct = string.Empty;
    public string MotherboardProduct
    {
        get => _motherboardProduct;
        set => SetProperty(ref _motherboardProduct, value);
    }

    private double _installedMemoryGB;
    public double InstalledMemoryGB
    {
        get => _installedMemoryGB;
        set => SetProperty(ref _installedMemoryGB, value);
    }

    private int _processCount;
    public int ProcessCount
    {
        get => _processCount;
        set => SetProperty(ref _processCount, value);
    }

    private int _threadCount;
    public int ThreadCount
    {
        get => _threadCount;
        set => SetProperty(ref _threadCount, value);
    }

    private long _handleCount;
    public long HandleCount
    {
        get => _handleCount;
        set => SetProperty(ref _handleCount, value);
    }

    private bool _staticInfoLoaded;

    public void Refresh()
    {
        // 静态信息只需加载一次
        if (!_staticInfoLoaded)
        {
            LoadStaticInfo();
            _staticInfoLoaded = true;
        }

        // 动态信息每次刷新
        UpdateDynamicInfo();
    }

    private void LoadStaticInfo()
    {
        try
        {
            // OS 信息
            var osResults = HardwareInfoService.ExecuteQuery("SELECT * FROM Win32_OperatingSystem");
            if (osResults.Count > 0)
            {
                var os = osResults[0];
                OsVersion = HardwareInfoService.GetStringProperty(os, "Caption");
                OsBuild = HardwareInfoService.GetStringProperty(os, "Version") + " (Build " +
                          HardwareInfoService.GetStringProperty(os, "BuildNumber") + ")";
                MachineName = HardwareInfoService.GetStringProperty(os, "CSName");
                InstalledMemoryGB = HardwareInfoService.BytesToGB(
                    HardwareInfoService.GetPropertyValue<ulong>(os, "TotalVisibleMemorySize") ?? 0 * 1024);
            }

            // BIOS 信息
            var biosResults = HardwareInfoService.ExecuteQuery("SELECT * FROM Win32_BIOS");
            if (biosResults.Count > 0)
            {
                var bios = biosResults[0];
                BiosVersion = HardwareInfoService.GetStringProperty(bios, "SMBIOSBIOSVersion");
                BiosManufacturer = HardwareInfoService.GetStringProperty(bios, "Manufacturer");
                if (string.IsNullOrEmpty(BiosVersion))
                    BiosVersion = HardwareInfoService.GetStringProperty(bios, "Version");
            }

            // 主板信息
            var mbResults = HardwareInfoService.ExecuteQuery("SELECT * FROM Win32_BaseBoard");
            if (mbResults.Count > 0)
            {
                var mb = mbResults[0];
                MotherboardManufacturer = HardwareInfoService.GetStringProperty(mb, "Manufacturer");
                MotherboardProduct = HardwareInfoService.GetStringProperty(mb, "Product");
            }
        }
        catch (Exception) { }

        // 环境信息 fallback
        if (string.IsNullOrEmpty(OsVersion))
            OsVersion = Environment.OSVersion.VersionString;
        if (string.IsNullOrEmpty(MachineName))
            MachineName = Environment.MachineName;
    }

    private void UpdateDynamicInfo()
    {
        // 运行时间
        Uptime = FormatUptime(Environment.TickCount64 / 1000);

        // 进程/线程/句柄计数
        try
        {
            var processes = Process.GetProcesses();
            ProcessCount = processes.Length;
            ThreadCount = processes.Sum(p =>
            {
                try { return p.Threads.Count; }
                catch { return 0; }
            });
            HandleCount = processes.Sum(p =>
            {
                try { return (long)p.HandleCount; }
                catch { return 0; }
            });
        }
        catch (Exception) { }
    }

    private static string FormatUptime(long totalSeconds)
    {
        var days = totalSeconds / 86400;
        var hours = (totalSeconds % 86400) / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        if (days > 0)
            return $"{days} 天 {hours:D2}:{minutes:D2}:{seconds:D2}";
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }
}
