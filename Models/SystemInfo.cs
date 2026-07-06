namespace WinUIApp.Models;

/// <summary>
/// 系统静态信息（OS版本、BIOS、运行时间等）
/// </summary>
public class SystemInfo
{
    public string OsVersion { get; set; } = string.Empty;
    public string OsBuild { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Uptime { get; set; } = string.Empty;
    public string BiosVersion { get; set; } = string.Empty;
    public string BiosManufacturer { get; set; } = string.Empty;
    public string MotherboardManufacturer { get; set; } = string.Empty;
    public string MotherboardProduct { get; set; } = string.Empty;
    public double InstalledMemoryGB { get; set; }
    public int ProcessCount { get; set; }
    public int ThreadCount { get; set; }
    public long HandleCount { get; set; }
}
