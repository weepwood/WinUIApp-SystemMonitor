namespace WinUIApp.Models;

/// <summary>
/// 内存总量和使用量数据
/// </summary>
public class MemoryInfo
{
    public double TotalGB { get; set; }
    public double UsedGB { get; set; }
    public double AvailableGB { get; set; }
    public float UsagePercent { get; set; }
    public double CommittedGB { get; set; }
    public double CachedGB { get; set; }
    public double PagedPoolGB { get; set; }
    public double NonPagedPoolGB { get; set; }
}
