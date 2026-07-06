namespace WinUIApp.Models;

/// <summary>
/// 单个磁盘驱动器的信息
/// </summary>
public class DiskInfo
{
    public string DriveName { get; set; } = string.Empty;
    public string VolumeLabel { get; set; } = string.Empty;
    public double TotalSizeGB { get; set; }
    public double FreeSpaceGB { get; set; }
    public double UsedSpaceGB { get; set; }
    public float UsagePercent { get; set; }
    public string DriveType { get; set; } = string.Empty;
    public string DriveFormat { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public double ReadSpeedMBps { get; set; }
    public double WriteSpeedMBps { get; set; }
}
