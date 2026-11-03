using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using WinUIApp.Models;
using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>
/// 磁盘信息 ViewModel
/// </summary>
public class DiskViewModel : BaseViewModel
{
    private readonly DiskService _service = new();

    public ObservableCollection<DiskItemViewModel> Drives { get; } = new();

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
    }
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

    private string _usagePercentText = "0.0%";
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
        set => SetProperty(ref _isReady, value);
    }

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

    private string _readSpeedText = "0 MB/s";
    public string ReadSpeedText
    {
        get => _readSpeedText;
        set => SetProperty(ref _readSpeedText, value);
    }

    private string _writeSpeedText = "0 MB/s";
    public string WriteSpeedText
    {
        get => _writeSpeedText;
        set => SetProperty(ref _writeSpeedText, value);
    }

    private string _sizeText = "0 GB / 0 GB";
    public string SizeText
    {
        get => _sizeText;
        set => SetProperty(ref _sizeText, value);
    }

    public void Update(Models.DiskInfo disk)
    {
        DriveName = disk.DriveName;
        IsReady = disk.IsReady;

        if (disk.IsReady)
        {
            DisplayName = string.IsNullOrEmpty(disk.VolumeLabel)
                ? $"{disk.DriveName} ({disk.DriveType})"
                : $"{disk.DriveName} {disk.VolumeLabel} ({disk.DriveType})";
            TotalSizeGB = disk.TotalSizeGB;
            FreeSpaceGB = disk.FreeSpaceGB;
            UsedSpaceGB = disk.UsedSpaceGB;
            UsagePercent = disk.UsagePercent;
            UsagePercentText = $"{UsagePercent:0.0}%";
            DriveType = disk.DriveType;
            ReadSpeedMBps = disk.ReadSpeedMBps;
            WriteSpeedMBps = disk.WriteSpeedMBps;

            ReadSpeedText = $"{ReadSpeedMBps:0.0} MB/s";
            WriteSpeedText = $"{WriteSpeedMBps:0.0} MB/s";
            SizeText = $"{UsedSpaceGB:0.0} GB / {TotalSizeGB:0.0} GB";
        }
        else
        {
            DisplayName = $"{disk.DriveName} (未就绪)";
            SizeText = "不可用";
        }
    }
}
