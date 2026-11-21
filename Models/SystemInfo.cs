namespace WinUIApp.Models;

/// <summary>
/// 系统静态信息（操作系统、固件、主板与显卡），进程统计由 ProcessSnapshot 提供。
/// </summary>
public class SystemInfo
{
    public string OsVersion { get; set; } = string.Empty;
    public string OsBuild { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string BiosVersion { get; set; } = string.Empty;
    public string BiosManufacturer { get; set; } = string.Empty;
    public string MotherboardManufacturer { get; set; } = string.Empty;
    public string MotherboardProduct { get; set; } = string.Empty;
    public string GraphicsName { get; set; } = string.Empty;
    public string GraphicsDriver { get; set; } = string.Empty;
    public double InstalledMemoryGB { get; set; }
}
