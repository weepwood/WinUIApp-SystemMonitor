using System;
using System.Diagnostics;
using WinUIApp.Models;
using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>
/// Separates slow hardware/process enumeration from UI-bound state updates.
/// </summary>
public sealed class SystemInfoViewModel : BaseViewModel
{
    private SystemInfo? _staticInfo;

    private string _osVersion = string.Empty;
    public string OsVersion { get => _osVersion; set => SetProperty(ref _osVersion, value); }

    private string _osBuild = string.Empty;
    public string OsBuild { get => _osBuild; set => SetProperty(ref _osBuild, value); }

    private string _machineName = string.Empty;
    public string MachineName { get => _machineName; set => SetProperty(ref _machineName, value); }

    private string _uptime = string.Empty;
    public string Uptime { get => _uptime; set => SetProperty(ref _uptime, value); }

    private string _biosVersion = string.Empty;
    public string BiosVersion { get => _biosVersion; set => SetProperty(ref _biosVersion, value); }

    private string _biosManufacturer = string.Empty;
    public string BiosManufacturer { get => _biosManufacturer; set => SetProperty(ref _biosManufacturer, value); }

    private string _motherboardManufacturer = string.Empty;
    public string MotherboardManufacturer { get => _motherboardManufacturer; set => SetProperty(ref _motherboardManufacturer, value); }

    private string _motherboardProduct = string.Empty;
    public string MotherboardProduct { get => _motherboardProduct; set => SetProperty(ref _motherboardProduct, value); }

    private double _installedMemoryGB;
    public double InstalledMemoryGB { get => _installedMemoryGB; set => SetProperty(ref _installedMemoryGB, value); }

    private int _processCount;
    public int ProcessCount { get => _processCount; set => SetProperty(ref _processCount, value); }

    private int _threadCount;
    public int ThreadCount { get => _threadCount; set => SetProperty(ref _threadCount, value); }

    private long _handleCount;
    public long HandleCount { get => _handleCount; set => SetProperty(ref _handleCount, value); }

    // Invoked by a single sampling worker at most once every five seconds.
    public SystemInfo Capture()
    {
        _staticInfo ??= LoadStaticInfo();
        var info = new SystemInfo
        {
            OsVersion = _staticInfo.OsVersion,
            OsBuild = _staticInfo.OsBuild,
            MachineName = _staticInfo.MachineName,
            BiosVersion = _staticInfo.BiosVersion,
            BiosManufacturer = _staticInfo.BiosManufacturer,
            MotherboardManufacturer = _staticInfo.MotherboardManufacturer,
            MotherboardProduct = _staticInfo.MotherboardProduct,
            InstalledMemoryGB = _staticInfo.InstalledMemoryGB,
        };

        try
        {
            var processes = Process.GetProcesses();
            info.ProcessCount = processes.Length;
            foreach (var process in processes)
            {
                using (process)
                {
                    try { info.ThreadCount += process.Threads.Count; } catch (Exception) { }
                    try { info.HandleCount += process.HandleCount; } catch (Exception) { }
                }
            }
        }
        catch (Exception) { }

        return info;
    }

    private static SystemInfo LoadStaticInfo()
    {
        var info = new SystemInfo();
        try
        {
            var osResults = HardwareInfoService.ExecuteQuery(
                "SELECT Caption, Version, BuildNumber, CSName, TotalVisibleMemorySize FROM Win32_OperatingSystem");
            if (osResults.Count > 0)
            {
                var os = osResults[0];
                info.OsVersion = HardwareInfoService.GetStringProperty(os, "Caption");
                info.OsBuild = HardwareInfoService.GetStringProperty(os, "Version") +
                    " (Build " + HardwareInfoService.GetStringProperty(os, "BuildNumber") + ")";
                info.MachineName = HardwareInfoService.GetStringProperty(os, "CSName");
                ulong totalKilobytes = HardwareInfoService.GetPropertyValue<ulong>(
                    os, "TotalVisibleMemorySize") ?? 0UL;
                info.InstalledMemoryGB = HardwareInfoService.BytesToGB(totalKilobytes * 1024UL);
            }

            var biosResults = HardwareInfoService.ExecuteQuery(
                "SELECT Manufacturer, SMBIOSBIOSVersion, Version FROM Win32_BIOS");
            if (biosResults.Count > 0)
            {
                var bios = biosResults[0];
                info.BiosVersion = HardwareInfoService.GetStringProperty(bios, "SMBIOSBIOSVersion");
                info.BiosManufacturer = HardwareInfoService.GetStringProperty(bios, "Manufacturer");
                if (string.IsNullOrEmpty(info.BiosVersion))
                    info.BiosVersion = HardwareInfoService.GetStringProperty(bios, "Version");
            }

            var boardResults = HardwareInfoService.ExecuteQuery(
                "SELECT Manufacturer, Product FROM Win32_BaseBoard");
            if (boardResults.Count > 0)
            {
                var board = boardResults[0];
                info.MotherboardManufacturer = HardwareInfoService.GetStringProperty(board, "Manufacturer");
                info.MotherboardProduct = HardwareInfoService.GetStringProperty(board, "Product");
            }
        }
        catch (Exception) { }

        if (string.IsNullOrWhiteSpace(info.OsVersion))
            info.OsVersion = Environment.OSVersion.VersionString;
        if (string.IsNullOrWhiteSpace(info.MachineName))
            info.MachineName = Environment.MachineName;
        return info;
    }

    public void Apply(SystemInfo info)
    {
        OsVersion = info.OsVersion;
        OsBuild = info.OsBuild;
        MachineName = info.MachineName;
        BiosVersion = info.BiosVersion;
        BiosManufacturer = info.BiosManufacturer;
        MotherboardManufacturer = info.MotherboardManufacturer;
        MotherboardProduct = info.MotherboardProduct;
        InstalledMemoryGB = info.InstalledMemoryGB;
        ProcessCount = info.ProcessCount;
        ThreadCount = info.ThreadCount;
        HandleCount = info.HandleCount;
    }

    public void UpdateUptime()
    {
        long totalSeconds = Environment.TickCount64 / 1000;
        long days = totalSeconds / 86400;
        long hours = totalSeconds % 86400 / 3600;
        long minutes = totalSeconds % 3600 / 60;
        long seconds = totalSeconds % 60;
        Uptime = days > 0
            ? $"{days} 天 {hours:D2}:{minutes:D2}:{seconds:D2}"
            : $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }
}
