using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WinUIApp.Models;
using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>
/// 磁盘信息 ViewModel，同时汇总所有就绪驱动器的容量数据。
/// </summary>
public class DiskViewModel : BaseViewModel
{
    private readonly DiskService _service = new();

    public ObservableCollection<DiskItemViewModel> Drives { get; } = new();

    private double _totalUsagePercent;
    public double TotalUsagePercent
    {
        get => _totalUsagePercent;
        private set => SetProperty(ref _totalUsagePercent, value);
    }

    private string _totalUsageText = "—";
    public string TotalUsageText
    {
        get => _totalUsageText;
        private set => SetProperty(ref _totalUsageText, value);
    }

    private string _totalSizeText = "—";
    public string TotalSizeText
    {
        get => _totalSizeText;
        private set => SetProperty(ref _totalSizeText, value);
    }

    private string _totalUsedText = "—";
    public string TotalUsedText
    {
        get => _totalUsedText;
        private set => SetProperty(ref _totalUsedText, value);
    }

    private string _totalFreeText = "—";
    public string TotalFreeText
    {
        get => _totalFreeText;
        private set => SetProperty(ref _totalFreeText, value);
    }

    private string _driveCountText = "0 个驱动器";
    public string DriveCountText
    {
        get => _driveCountText;
        private set => SetProperty(ref _driveCountText, value);
    }

    private bool _hasDrives;
    public bool HasDrives
    {
        get => _hasDrives;
        private set => SetProperty(ref _hasDrives, value);
    }

    public List<DiskInfo> Capture() => _service.GetDiskInfos();

    public void Apply(IReadOnlyList<DiskInfo> disks)
    {
        foreach (var disk in disks)
        {
            var existing = Drives.FirstOrDefault(d => d.DriveName == disk.DriveName);
            if (existing != null)
            {
                existing.Update(disk);
            }
            else
            {
                var vm = new DiskItemViewModel();
                vm.Update(disk);
                Drives.Add(vm);
            }
        }

        // 移除已不存在的驱动器
        var currentNames = disks.Select(d => d.DriveName).ToHashSet();
        for (int i = Drives.Count - 1; i >= 0; i--)
        {
            if (!currentNames.Contains(Drives[i].DriveName))
                Drives.RemoveAt(i);
        }

        RefreshAggregates();
    }

    private void RefreshAggregates()
    {
        var ready = Drives.Where(d => d.IsReady).ToList();
        double total = ready.Sum(d => d.TotalSizeGB);
        double used = ready.Sum(d => d.UsedSpaceGB);
        double free = ready.Sum(d => d.FreeSpaceGB);

        TotalUsagePercent = total > 0 ? Math.Min(100, used / total * 100) : 0;
        TotalUsageText = total > 0 ? $"{TotalUsagePercent:0.0}" : "—";
        TotalSizeText = total > 0 ? FormatGb(total) : "—";
        TotalUsedText = total > 0 ? FormatGb(used) : "—";
        TotalFreeText = total > 0 ? FormatGb(free) : "—";
        DriveCountText = $"{Drives.Count} 个驱动器";
        HasDrives = Drives.Count > 0;
    }

    public static string FormatGb(double gigabytes) =>
        gigabytes >= 1024
            ? $"{gigabytes / 1024:0.00} TB"
            : $"{gigabytes:0.0} GB";
}

public class DiskItemViewModel : BaseViewModel
{
    public string DriveName { get; set; } = string.Empty;

    private string _displayName = string.Empty;
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    private double _totalSizeGB;
    public double TotalSizeGB
    {
        get => _totalSizeGB;
        set => SetProperty(ref _totalSizeGB, value);
    }

    private double _freeSpaceGB;
    public double FreeSpaceGB
    {
        get => _freeSpaceGB;
        set => SetProperty(ref _freeSpaceGB, value);
    }

    private double _usedSpaceGB;
    public double UsedSpaceGB
    {
        get => _usedSpaceGB;
        set => SetProperty(ref _usedSpaceGB, value);
    }

    private float _usagePercent;
    public float UsagePercent
    {
        get => _usagePercent;
        set
        {
            if (SetProperty(ref _usagePercent, value))
                OnPropertyChanged(nameof(UsagePercentText));
        }
    }

    private string _usagePercentText = "—";
    public string UsagePercentText
    {
        get => _usagePercentText;
        set => SetProperty(ref _usagePercentText, value);
    }

    private string _driveType = string.Empty;
    public string DriveType
    {
        get => _driveType;
        set => SetProperty(ref _driveType, value);
    }

    private bool _isReady;
    public bool IsReady
    {
        get => _isReady;
        set
        {
            if (SetProperty(ref _isReady, value))
                OnPropertyChanged(nameof(StatusText));
        }
    }

    public string StatusText => IsReady ? "就绪" : "未就绪";

    private double _readSpeedMBps;
    public double ReadSpeedMBps
    {
        get => _readSpeedMBps;
        set => SetProperty(ref _readSpeedMBps, value);
    }

    private double _writeSpeedMBps;
    public double WriteSpeedMBps
    {
        get => _writeSpeedMBps;
        set => SetProperty(ref _writeSpeedMBps, value);
    }

    private string _readSpeedText = "—";
    public string ReadSpeedText
    {
        get => _readSpeedText;
        set => SetProperty(ref _readSpeedText, value);
    }

    private string _writeSpeedText = "—";
    public string WriteSpeedText
    {
        get => _writeSpeedText;
        set => SetProperty(ref _writeSpeedText, value);
    }

    private string _sizeText = "—";
    public string SizeText
    {
        get => _sizeText;
        set => SetProperty(ref _sizeText, value);
    }

    private string _freeSpaceText = "—";
    public string FreeSpaceText
    {
        get => _freeSpaceText;
        set => SetProperty(ref _freeSpaceText, value);
    }

    public void Update(Models.DiskInfo disk)
    {
        DriveName = disk.DriveName;
        IsReady = disk.IsReady;
        DriveType = disk.DriveType;

        if (disk.IsReady)
        {
            DisplayName = string.IsNullOrEmpty(disk.VolumeLabel)
                ? $"{disk.DriveName.TrimEnd('\\')} · {disk.DriveType}"
                : $"{disk.DriveName.TrimEnd('\\')} · {disk.VolumeLabel}";
            TotalSizeGB = disk.TotalSizeGB;
            FreeSpaceGB = disk.FreeSpaceGB;
            UsedSpaceGB = disk.UsedSpaceGB;
            UsagePercent = disk.UsagePercent;
            UsagePercentText = $"{UsagePercent:0.0}%";
            ReadSpeedMBps = disk.ReadSpeedMBps;
            WriteSpeedMBps = disk.WriteSpeedMBps;

            ReadSpeedText = $"{ReadSpeedMBps:0.0} MB/s";
            WriteSpeedText = $"{WriteSpeedMBps:0.0} MB/s";
            SizeText = $"{DiskViewModel.FormatGb(UsedSpaceGB)} / {DiskViewModel.FormatGb(TotalSizeGB)}";
            FreeSpaceText = $"剩余 {DiskViewModel.FormatGb(FreeSpaceGB)}";
        }
        else
        {
            DisplayName = $"{disk.DriveName.TrimEnd('\\')} · {disk.DriveType}";
            UsagePercent = 0;
            UsagePercentText = "—";
            TotalSizeGB = 0;
            UsedSpaceGB = 0;
            FreeSpaceGB = 0;
            ReadSpeedMBps = 0;
            WriteSpeedMBps = 0;
            ReadSpeedText = "—";
            WriteSpeedText = "—";
            SizeText = "不可用";
            FreeSpaceText = "—";
        }
    }
}
