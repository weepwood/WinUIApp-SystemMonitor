using System;
using System.Collections.Generic;
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

    private string _graphicsName = "—";
    public string GraphicsName { get => _graphicsName; set => SetProperty(ref _graphicsName, value); }

    private string _graphicsDriver = "—";
    public string GraphicsDriver { get => _graphicsDriver; set => SetProperty(ref _graphicsDriver, value); }

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

    private string _selfOverheadText = "—";
    public string SelfOverheadText
    {
        get => _selfOverheadText;
        private set => SetProperty(ref _selfOverheadText, value);
    }

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

    /// <summary>读取操作系统、固件、主板与显卡等静态信息。由后台采样线程调用。</summary>
    public SystemInfo Capture()
    {
        _staticInfo ??= LoadStaticInfo();
        return new SystemInfo
        {
            OsVersion = _staticInfo.OsVersion,
            OsBuild = _staticInfo.OsBuild,
            MachineName = _staticInfo.MachineName,
            BiosVersion = _staticInfo.BiosVersion,
            BiosManufacturer = _staticInfo.BiosManufacturer,
            MotherboardManufacturer = _staticInfo.MotherboardManufacturer,
            MotherboardProduct = _staticInfo.MotherboardProduct,
            GraphicsName = _staticInfo.GraphicsName,
            GraphicsDriver = _staticInfo.GraphicsDriver,
            InstalledMemoryGB = _staticInfo.InstalledMemoryGB,
        };
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

            var graphicsResults = HardwareInfoService.ExecuteQuery(
                "SELECT Name, DriverVersion FROM Win32_VideoController");
            if (graphicsResults.Count > 0)
            {
                var names = graphicsResults
                    .Select(g => HardwareInfoService.GetStringProperty(g, "Name").Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToArray();
                var drivers = graphicsResults
                    .Select(g => HardwareInfoService.GetStringProperty(g, "DriverVersion").Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToArray();

                if (names.Length > 0)
                    info.GraphicsName = string.Join(" · ", names);
                if (drivers.Length > 0)
                    info.GraphicsDriver = string.Join(" · ", drivers);
            }
        }
        catch (Exception) { }

        if (string.IsNullOrWhiteSpace(info.OsVersion))
            info.OsVersion = Environment.OSVersion.VersionString;
        if (string.IsNullOrWhiteSpace(info.MachineName))
            info.MachineName = Environment.MachineName;
        return info;
    }

    // Called on the UI thread after a slow sample.
    public void ApplyStatic(SystemInfo info)
    {
        OsVersion = info.OsVersion;
        OsBuild = info.OsBuild;
        MachineName = info.MachineName;
        BiosVersion = info.BiosVersion;
        BiosManufacturer = info.BiosManufacturer;
        MotherboardManufacturer = info.MotherboardManufacturer;
        MotherboardProduct = info.MotherboardProduct;
        GraphicsName = string.IsNullOrWhiteSpace(info.GraphicsName) ? "—" : info.GraphicsName;
        GraphicsDriver = string.IsNullOrWhiteSpace(info.GraphicsDriver) ? "—" : info.GraphicsDriver;
        InstalledMemoryGB = info.InstalledMemoryGB;
        OnPropertyChanged(nameof(InstalledMemoryText));
    }

    // Called on the UI thread after a slow process sample.
    public void ApplyProcessSnapshot(ProcessSnapshot snapshot)
    {
        ProcessCount = snapshot.ProcessCount;
        ThreadCount = snapshot.ThreadCount;
        HandleCount = snapshot.HandleCount;
        UpdateTopProcesses(snapshot.TopByMemory);
    }

    private void UpdateTopProcesses(IReadOnlyList<ProcessInfo> processes)
    {
        if (processes.Count == 0)
        {
            TopProcesses.Clear();
            return;
        }

        long max = processes.Max(p => p.WorkingSetBytes);
        TopProcesses.Clear();
        for (int i = 0; i < processes.Count; i++)
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

    /// <summary>本应用自身的 CPU 与工作集，用于衡量监测开销。</summary>
    public void ApplySelfMetrics(float cpuPercent, long workingSetBytes)
    {
        SelfOverheadText = $"{cpuPercent:0.00}% · {ProcessInfo.FormatBytes(workingSetBytes)}";
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
