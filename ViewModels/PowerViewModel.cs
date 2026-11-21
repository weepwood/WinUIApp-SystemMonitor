using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>电源 / 电池页的状态，来自 GetSystemPowerStatus。</summary>
public sealed class PowerViewModel : BaseViewModel
{
    private bool _hasBattery;
    private bool _isAcOnline;
    private bool _isCharging;
    private int _batteryPercent = -1;
    private string _summary = "电源状态未知";

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public bool HasBattery
    {
        get => _hasBattery;
        private set
        {
            if (SetProperty(ref _hasBattery, value))
            {
                OnPropertyChanged(nameof(BatteryLevelText));
                OnPropertyChanged(nameof(BatteryGaugeValue));
                OnPropertyChanged(nameof(BatteryPercentText));
            }
        }
    }

    public bool IsAcOnline
    {
        get => _isAcOnline;
        private set
        {
            if (SetProperty(ref _isAcOnline, value))
            {
                OnPropertyChanged(nameof(PowerSourceText));
            }
        }
    }

    public bool IsCharging
    {
        get => _isCharging;
        private set
        {
            if (SetProperty(ref _isCharging, value))
                OnPropertyChanged(nameof(ChargingStatusText));
        }
    }

    public int BatteryPercent
    {
        get => _batteryPercent;
        private set
        {
            if (SetProperty(ref _batteryPercent, value))
            {
                OnPropertyChanged(nameof(BatteryPercentText));
                OnPropertyChanged(nameof(BatteryLevelText));
                OnPropertyChanged(nameof(BatteryGaugeValue));
                OnPropertyChanged(nameof(BatteryStatusText));
            }
        }
    }

    public float BatteryGaugeValue => HasBattery && BatteryPercent >= 0 ? BatteryPercent : 0;

    public string BatteryPercentText => HasBattery && BatteryPercent >= 0 ? $"{BatteryPercent}%" : "—";

    public string BatteryLevelText => HasBattery ? BatteryPercentText : "无电池";

    public string PowerSourceText => IsAcOnline ? "交流电" : "电池";

    public string ChargingStatusText => !HasBattery
        ? "不适用"
        : IsCharging ? "充电中" : IsAcOnline ? "已连接电源" : "放电中";

    public string BatteryStatusText => !HasBattery
        ? "无电池"
        : IsCharging ? "充电中"
        : BatteryPercent >= 0 ? (BatteryPercent >= 20 ? "电量正常" : "电量偏低")
        : "状态未知";

    public void Apply(PowerStatus status)
    {
        HasBattery = status.HasBattery;
        IsAcOnline = status.IsAcOnline;
        IsCharging = status.IsCharging;
        BatteryPercent = status.BatteryPercent;
        Summary = status.Summary;
    }
}
