using WinUIApp.Models;
using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>
/// 内存使用量数据 ViewModel
/// </summary>
public class MemoryViewModel : BaseViewModel
{
    private readonly MemoryService _service = new();

    private double _totalGB;
    public double TotalGB
    {
        get => _totalGB;
        set => SetProperty(ref _totalGB, value);
    }

    private double _usedGB;
    public double UsedGB
    {
        get => _usedGB;
        set => SetProperty(ref _usedGB, value);
    }

    private double _availableGB;
    public double AvailableGB
    {
        get => _availableGB;
        set => SetProperty(ref _availableGB, value);
    }

    private float _usagePercent;
    public float UsagePercent
    {
        get => _usagePercent;
        set => SetProperty(ref _usagePercent, value);
    }

    private double _committedGB;
    public double CommittedGB
    {
        get => _committedGB;
        set => SetProperty(ref _committedGB, value);
    }

    private double _cachedGB;
    public double CachedGB
    {
        get => _cachedGB;
        set => SetProperty(ref _cachedGB, value);
    }

    private double _pagedPoolGB;
    public double PagedPoolGB
    {
        get => _pagedPoolGB;
        set => SetProperty(ref _pagedPoolGB, value);
    }

    private double _nonPagedPoolGB;
    public double NonPagedPoolGB
    {
        get => _nonPagedPoolGB;
        set => SetProperty(ref _nonPagedPoolGB, value);
    }

    private string _usageText = "0%";
    public string UsageText
    {
        get => _usageText;
        set => SetProperty(ref _usageText, value);
    }

    private string _totalText = "0 GB";
    public string TotalText
    {
        get => _totalText;
        set => SetProperty(ref _totalText, value);
    }

    private string _availableText = "0 GB";
    public string AvailableText
    {
        get => _availableText;
        set => SetProperty(ref _availableText, value);
    }

    public MemoryInfo Capture() => _service.GetMemoryInfo();

    // All notifications occur on the UI thread.
    public void Apply(MemoryInfo info)
    {

        TotalGB = info.TotalGB;
        UsedGB = info.UsedGB;
        AvailableGB = info.AvailableGB;
        UsagePercent = info.UsagePercent;
        CommittedGB = info.CommittedGB;
        CachedGB = info.CachedGB;
        PagedPoolGB = info.PagedPoolGB;
        NonPagedPoolGB = info.NonPagedPoolGB;

        UsageText = $"{UsagePercent:0.0}%";
        TotalText = $"{TotalGB:0.0} GB";
        AvailableText = $"{AvailableGB:0.0} GB";
    }
}
