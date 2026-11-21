namespace WinUIApp.ViewModels;

/// <summary>趋势图的显示选项，直接绑定到图表控件。</summary>
public sealed class ChartOptions : BaseViewModel
{
    private int _historyCapacity = 60;
    public int HistoryCapacity
    {
        get => _historyCapacity;
        set => SetProperty(ref _historyCapacity, value);
    }

    private bool _showChartGrid = true;
    public bool ShowChartGrid
    {
        get => _showChartGrid;
        set => SetProperty(ref _showChartGrid, value);
    }

    private bool _showChartArea = true;
    public bool ShowChartArea
    {
        get => _showChartArea;
        set => SetProperty(ref _showChartArea, value);
    }

    private bool _showThresholdLine;
    public bool ShowThresholdLine
    {
        get => _showThresholdLine;
        set => SetProperty(ref _showThresholdLine, value);
    }

    private double _threshold = 80;
    public double Threshold
    {
        get => _threshold;
        set => SetProperty(ref _threshold, value);
    }
}
