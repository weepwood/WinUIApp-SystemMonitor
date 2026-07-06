using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinUIApp.Models;

namespace WinUIApp.Services;

/// <summary>
/// 磁盘信息采集服务（使用 DriveInfo + WMI）
/// </summary>
public class DiskService
{
    private readonly Dictionary<string, (long readBytes, long writeBytes, DateTime time)> _previousStats = new();

    public List<DiskInfo> GetDiskInfos()
    {
        var disks = new List<DiskInfo>();
        var drives = DriveInfo.GetDrives();

        foreach (var drive in drives)
        {
            var disk = new DiskInfo
            {
                DriveName = drive.Name,
                IsReady = drive.IsReady,
            };

            if (drive.IsReady)
            {
                disk.VolumeLabel = drive.VolumeLabel;
                disk.DriveFormat = drive.DriveFormat;
                disk.TotalSizeGB = HardwareInfoService.BytesToGB((ulong)drive.TotalSize);
                disk.FreeSpaceGB = HardwareInfoService.BytesToGB((ulong)drive.TotalFreeSpace);
                disk.UsedSpaceGB = disk.TotalSizeGB - disk.FreeSpaceGB;
                disk.UsagePercent = disk.TotalSizeGB > 0
                    ? (float)Math.Round(disk.UsedSpaceGB / disk.TotalSizeGB * 100, 1)
                    : 0;
                disk.DriveType = drive.DriveType switch
                {
                    System.IO.DriveType.Fixed => "本地磁盘",
                    System.IO.DriveType.Removable => "可移动磁盘",
                    System.IO.DriveType.Network => "网络驱动器",
                    System.IO.DriveType.CDRom => "光驱",
                    System.IO.DriveType.Ram => "RAM 磁盘",
                    _ => "未知"
                };
            }

            disks.Add(disk);
        }

        // 更新读写速率
        UpdateDiskSpeeds(disks);

        return disks;
    }

    private void UpdateDiskSpeeds(List<DiskInfo> disks)
    {
        try
        {
            var results = HardwareInfoService.ExecuteQuery(
                "SELECT * FROM Win32_PerfFormattedData_PerfDisk_LogicalDisk WHERE Name LIKE '_Total'");

            // 如果没有 _Total，尝试获取每个磁盘的
            if (results.Count == 0)
            {
                results = HardwareInfoService.ExecuteQuery(
                    "SELECT * FROM Win32_PerfFormattedData_PerfDisk_LogicalDisk");
            }

            var now = DateTime.UtcNow;

            foreach (var obj in results)
            {
                string name = HardwareInfoService.GetStringProperty(obj, "Name");
                long readBytes = (long)(HardwareInfoService.GetPropertyValue<ulong>(obj, "DiskReadBytesPerSec") ?? 0);
                long writeBytes = (long)(HardwareInfoService.GetPropertyValue<ulong>(obj, "DiskWriteBytesPerSec") ?? 0);

                if (_previousStats.TryGetValue(name, out var prev))
                {
                    double elapsedSec = (now - prev.time).TotalSeconds;
                    if (elapsedSec > 0)
                    {
                        double readSpeed = HardwareInfoService.BytesToMB(readBytes) / elapsedSec;
                        double writeSpeed = HardwareInfoService.BytesToMB(writeBytes) / elapsedSec;

                        var disk = disks.FirstOrDefault(d => d.DriveName.TrimEnd('\\') == name.TrimEnd('_'));
                        if (disk != null || name == "_Total")
                        {
                            if (name == "_Total" && disks.Count > 0)
                            {
                                // 将总速度分配给第一个有数据的盘
                                var firstReady = disks.FirstOrDefault(d => d.IsReady);
                                if (firstReady != null)
                                {
                                    firstReady.ReadSpeedMBps = Math.Abs(readSpeed);
                                    firstReady.WriteSpeedMBps = Math.Abs(writeSpeed);
                                }
                            }
                            else if (disk != null)
                            {
                                disk.ReadSpeedMBps = Math.Abs(readSpeed);
                                disk.WriteSpeedMBps = Math.Abs(writeSpeed);
                            }
                        }
                    }
                }

                _previousStats[name] = (readBytes, writeBytes, now);
            }
        }
        catch (Exception) { }
    }
}
