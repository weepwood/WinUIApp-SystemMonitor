using WinUIApp.Models;
using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>
/// 内存使用量数据 ViewModel
/// </summary>
public class MemoryViewModel : BaseViewModel
{
    private readonly MemoryService _service = new();

    public UsageHistory History { get; } = new();

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

    private string _usageText = "—";
    public string UsageText
    {
        get => _usageText;
        set => SetProperty(ref _usageText, value);
    }

    private string _usageValueText = "—";
    public string UsageValueText
    {
        get => _usageValueText;
        set => SetProperty(ref _usageValueText, value);
    }

    private string _totalText = "—";
    public string TotalText
    {
        get => _totalText;
        set => SetProperty(ref _totalText, value);
    }

    private string _availableText = "—";
    public string AvailableText
    {
        get => _availableText;
        set => SetProperty(ref _availableText, value);
    }

    private string _usedText = "—";
    public string UsedText
    {
        get => _usedText;
        set => SetProperty(ref _usedText, value);
    }

    private string _committedText = "—";
    public string CommittedText
    {
        get => _committedText;
        set => SetProperty(ref _committedText, value);
    }

    private string _cachedText = "—";
    public string CachedText
    {
        get => _cachedText;
        set => SetProperty(ref _cachedText, value);
    }

    private string _pagedPoolText = "—";
    public string PagedPoolText
    {
        get => _pagedPoolText;
        set => SetProperty(ref _pagedPoolText, value);
    }

    private string _nonPagedPoolText = "—";
    public string NonPagedPoolText
    {
        get => _nonPagedPoolText;
        set => SetProperty(ref _nonPagedPoolText, value);
    }

    private string _availableOfTotalText = "—";
    public string AvailableOfTotalText
    {
        get => _availableOfTotalText;
        set => SetProperty(ref _availableOfTotalText, value);
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

        History.Append(UsagePercent);
        UsageText = $"{UsagePercent:0.0}%";
        UsageValueText = $"{UsagePercent:0.0}";
        TotalText = $"{TotalGB:0.0} GB";
        AvailableText = $"{AvailableGB:0.0} GB";
        UsedText = $"{UsedGB:0.0} GB";
        CommittedText = $"{CommittedGB:0.0} GB";
        CachedText = $"{CachedGB:0.0} GB";
        PagedPoolText = $"{PagedPoolGB:0.00} GB";
        NonPagedPoolText = $"{NonPagedPoolGB:0.00} GB";
        AvailableOfTotalText = $"可用 {AvailableText} / {TotalText}";
    }
}
