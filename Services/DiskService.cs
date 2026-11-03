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
                        ? (float)Math.Clamp(Math.Round(disk.UsedSpaceGB / disk.TotalSizeGB * 100, 1), 0, 100)
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
        try
        {
            var counters = HardwareInfoService.ExecuteQuery(
                "SELECT Name, DiskReadBytesPerSec, DiskWriteBytesPerSec FROM Win32_PerfFormattedData_PerfDisk_LogicalDisk WHERE Name <> '_Total'");
            foreach (var counter in counters)
            {
                string name = HardwareInfoService.GetStringProperty(counter, "Name");
                var disk = disks.FirstOrDefault(d => d.IsReady &&
                    string.Equals(d.DriveName.TrimEnd('\\'), name, StringComparison.OrdinalIgnoreCase));
                if (disk == null) continue;

                ulong readBytesPerSecond = HardwareInfoService.GetPropertyValue<ulong>(
                    counter, "DiskReadBytesPerSec") ?? 0;
                ulong writeBytesPerSecond = HardwareInfoService.GetPropertyValue<ulong>(
                    counter, "DiskWriteBytesPerSec") ?? 0;
                disk.ReadSpeedMBps = Math.Round(readBytesPerSecond / 1_048_576.0, 2);
                disk.WriteSpeedMBps = Math.Round(writeBytesPerSecond / 1_048_576.0, 2);
            }
        }
        catch (Exception)
        {
            // Hardware counters can be unavailable or disabled. Capacity remains usable.
        }
    }
}
