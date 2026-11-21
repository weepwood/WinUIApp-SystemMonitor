using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinUIApp.Models;

namespace WinUIApp.Services;

/// <summary>
/// Drive capacities from DriveInfo and instantaneous logical disk throughput from WMI.
/// Formatted counter values are already bytes/second; never divide by elapsed time.
/// </summary>
public sealed class DiskService
{
    public List<DiskInfo> GetDiskInfos()
    {
        var disks = new List<DiskInfo>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            var disk = new DiskInfo { DriveName = drive.Name };
            try
            {
                disk.IsReady = drive.IsReady;
                if (disk.IsReady)
                {
                    disk.VolumeLabel = drive.VolumeLabel;
                    disk.DriveFormat = drive.DriveFormat;
                    disk.TotalSizeGB = HardwareInfoService.BytesToGB((ulong)drive.TotalSize);
                    disk.FreeSpaceGB = HardwareInfoService.BytesToGB((ulong)drive.TotalFreeSpace);
                    disk.UsedSpaceGB = Math.Max(0, disk.TotalSizeGB - disk.FreeSpaceGB);
                    disk.UsagePercent = disk.TotalSizeGB > 0
                        ? (float)Math.Min(100, Math.Round(disk.UsedSpaceGB / disk.TotalSizeGB * 100, 1))
                        : 0;
                }
                disk.DriveType = drive.DriveType switch
                {
                    DriveType.Fixed => "本地磁盘",
                    DriveType.Removable => "可移动磁盘",
                    DriveType.Network => "网络驱动器",
                    DriveType.CDRom => "光驱",
                    DriveType.Ram => "RAM 磁盘",
                    _ => "未知"
                };
            }
            catch (Exception)
            {
                disk.IsReady = false;
            }
            disks.Add(disk);
        }

        UpdateDiskSpeeds(disks);
        return disks;
    }

    private static void UpdateDiskSpeeds(List<DiskInfo> disks)
    {
        // 空闲磁盘的速率是 0；计数器不可用时则保持 SpeedAvailable=false。
        // 预建索引，避免每一行 WMI 结果都扫描一次驱动器列表。
        var ready = new Dictionary<string, DiskInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var disk in disks)
        {
            disk.SpeedAvailable = false;
            if (disk.IsReady)
                ready[disk.DriveName.TrimEnd('\\')] = disk;
        }

        try
        {
            var counters = HardwareInfoService.ExecuteQuery(
                "SELECT Name, DiskReadBytesPerSec, DiskWriteBytesPerSec FROM Win32_PerfFormattedData_PerfDisk_LogicalDisk WHERE Name <> '_Total'");
            foreach (var counter in counters)
            {
                string name = HardwareInfoService.GetStringProperty(counter, "Name");
                if (!ready.TryGetValue(name, out var disk))
                    continue;

                ulong? read = HardwareInfoService.GetPropertyValue<ulong>(counter, "DiskReadBytesPerSec");
                ulong? write = HardwareInfoService.GetPropertyValue<ulong>(counter, "DiskWriteBytesPerSec");
                if (!read.HasValue || !write.HasValue)
                    continue;

                disk.ReadSpeedMBps = Math.Round(read.Value / 1_048_576.0, 2);
                disk.WriteSpeedMBps = Math.Round(write.Value / 1_048_576.0, 2);
                disk.SpeedAvailable = true;
            }
        }
        catch (Exception)
        {
            // WMI 或性能计数器故障时仅影响实时速度，容量仍可显示。
        }
    }
}
