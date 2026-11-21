using System;
using System.Collections.ObjectModel;
using System.Linq;
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

    private string _machineName = "—";
    public string MachineName { get => _machineName; set => SetProperty(ref _machineName, value); }

    private string _uptime = "—";
    public string Uptime { get => _uptime; set => SetProperty(ref _uptime, value); }

    private string _biosVersion = "—";
    public string BiosVersion { get => _biosVersion; set => SetProperty(ref _biosVersion, value); }

    private string _biosManufacturer = "—";
    public string BiosManufacturer { get => _biosManufacturer; set => SetProperty(ref _biosManufacturer, value); }

    private string _motherboardManufacturer = "—";
    public string MotherboardManufacturer { get => _motherboardManufacturer; set => SetProperty(ref _motherboardManufacturer, value); }

    private string _motherboardProduct = "—";
    public string MotherboardProduct { get => _motherboardProduct; set => SetProperty(ref _motherboardProduct, value); }

    private double _installedMemoryGB;
    public double InstalledMemoryGB { get => _installedMemoryGB; set => SetProperty(ref _installedMemoryGB, value); }

    public string InstalledMemoryText => InstalledMemoryGB > 0 ? $"{InstalledMemoryGB:0.#} GB" : "—";

    private int _processCount;
    public int ProcessCount
    {
        get => _processCount;
        set
        {
            if (SetProperty(ref _processCount, value))
            {
                OnPropertyChanged(nameof(ProcessThreadSummary));
                OnPropertyChanged(nameof(ProcessCountText));
            }
        }
    }

    private int _threadCount;
    public int ThreadCount
    {
        get => _threadCount;
        set
        {
            if (SetProperty(ref _threadCount, value))
            {
                OnPropertyChanged(nameof(ProcessThreadSummary));
                OnPropertyChanged(nameof(ThreadCountText));
            }
        }
    }

    public string ProcessThreadSummary => $"{ProcessCount} / {ThreadCount}";
    public string ProcessCountText => ProcessCount.ToString("N0");
    public string ThreadCountText => ThreadCount.ToString("N0");

    private long _handleCount;
    public long HandleCount
    {
        get => _handleCount;
        set
        {
            if (SetProperty(ref _handleCount, value))
                OnPropertyChanged(nameof(HandleCountText));
        }
    }

    public string HandleCountText => HandleCount > 0 ? HandleCount.ToString("N0") : "—";

    private int _topProcessCount = 5;
    public int TopProcessCount
    {
        get => _topProcessCount;
        set
        {
            if (SetProperty(ref _topProcessCount, value))
                OnPropertyChanged(nameof(TopProcessesTitle));
        }
    }

    public string TopProcessesTitle => $"内存占用前 {TopProcessCount} 的进程";

    public ObservableCollection<ProcessRowViewModel> TopProcesses { get; } = new();

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
            ProcessService.Sample(info, TopProcessCount);
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
        OnPropertyChanged(nameof(InstalledMemoryText));

        UpdateTopProcesses(info.TopProcesses);

        ProcessCount = info.ProcessCount;
        ThreadCount = info.ThreadCount;
        HandleCount = info.HandleCount;
    }

    private void UpdateTopProcesses(ProcessMemoryInfo[] processes)
    {
        if (processes.Length == 0)
        {
            TopProcesses.Clear();
            return;
        }

        long max = processes.Max(p => p.WorkingSetBytes);
        TopProcesses.Clear();
        for (int i = 0; i < processes.Length; i++)
        {
            var process = processes[i];
            TopProcesses.Add(new ProcessRowViewModel
            {
                Rank = i + 1,
                Name = process.Name,
                Id = process.Id,
                SizeText = process.SizeText,
                SharePercent = max > 0 ? process.WorkingSetBytes * 100.0 / max : 0,
            });
        }
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

public sealed class ProcessRowViewModel
{
    public int Rank { get; set; }
    public string RankText => Rank.ToString();
    public string Name { get; set; } = string.Empty;
    public int Id { get; set; }
    public string SizeText { get; set; } = string.Empty;
    public double SharePercent { get; set; }
}
