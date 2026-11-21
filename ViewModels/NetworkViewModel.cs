using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WinUIApp.Models;
using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>
/// 网络信息 ViewModel，同时汇总所有网卡的上下行速率。
/// </summary>
public class NetworkViewModel : BaseViewModel
{
    private readonly NetworkService _service = new();

    public ObservableCollection<NetworkItemViewModel> Adapters { get; } = new();

    private double _totalDownloadMbps;
    public double TotalDownloadMbps
    {
        get => _totalDownloadMbps;
        private set => SetProperty(ref _totalDownloadMbps, value);
    }

    private double _totalUploadMbps;
    public double TotalUploadMbps
    {
        get => _totalUploadMbps;
        private set => SetProperty(ref _totalUploadMbps, value);
    }

    private string _downloadValueText = "0";
    public string DownloadValueText
    {
        get => _downloadValueText;
        private set => SetProperty(ref _downloadValueText, value);
    }

    private string _downloadUnitText = "Mbps";
    public string DownloadUnitText
    {
        get => _downloadUnitText;
        private set => SetProperty(ref _downloadUnitText, value);
    }

    private string _downloadFullText = "0 bps";
    public string DownloadFullText
    {
        get => _downloadFullText;
        private set => SetProperty(ref _downloadFullText, value);
    }

    private string _uploadText = "0 bps";
    public string UploadText
    {
        get => _uploadText;
        private set => SetProperty(ref _uploadText, value);
    }

    private string _adapterCountText = "0 个网卡";
    public string AdapterCountText
    {
        get => _adapterCountText;
        private set => SetProperty(ref _adapterCountText, value);
    }

    private string _activeAdapterText = "0 个网卡";
    public string ActiveAdapterText
    {
        get => _activeAdapterText;
        private set => SetProperty(ref _activeAdapterText, value);
    }

    private string _primaryAdapterText = "未检测到网卡";
    public string PrimaryAdapterText
    {
        get => _primaryAdapterText;
        private set => SetProperty(ref _primaryAdapterText, value);
    }

    private bool _hasAdapters;
    public bool HasAdapters
    {
        get => _hasAdapters;
        private set => SetProperty(ref _hasAdapters, value);
    }

    public List<NetworkInfo> Capture() => _service.GetNetworkInfos();

    public void Apply(IReadOnlyList<NetworkInfo> networks)
    {
        var activeAdapters = networks.Where(n => n.IsActive || !string.IsNullOrEmpty(n.Description)).ToList();

        foreach (var net in activeAdapters)
        {
            var existing = Adapters.FirstOrDefault(a => a.AdapterName == net.AdapterName);
            if (existing != null)
            {
                existing.Update(net);
            }
            else
            {
                var vm = new NetworkItemViewModel();
                vm.Update(net);
                Adapters.Add(vm);
            }
        }

        // 清理已消失的适配器
        var currentNames = activeAdapters.Select(n => n.AdapterName).ToHashSet();
        for (int i = Adapters.Count - 1; i >= 0; i--)
        {
            if (!currentNames.Contains(Adapters[i].AdapterName))
                Adapters.RemoveAt(i);
        }

        RefreshAggregates();
    }

    private void RefreshAggregates()
    {
        TotalDownloadMbps = Adapters.Sum(a => a.DownloadSpeedMbps);
        TotalUploadMbps = Adapters.Sum(a => a.UploadSpeedMbps);

        var (value, unit) = HardwareInfoService.FormatSpeedParts(TotalDownloadMbps);
        DownloadValueText = value;
        DownloadUnitText = unit;
        DownloadFullText = HardwareInfoService.FormatSpeed(TotalDownloadMbps);
        UploadText = HardwareInfoService.FormatSpeed(TotalUploadMbps);

        int active = Adapters.Count(a => a.IsActive);
        ActiveAdapterText = $"{Adapters.Count} 个网卡 · {active} 个已连接";
        AdapterCountText = $"{active}/{Adapters.Count} 已连接";
        PrimaryAdapterText = (Adapters.FirstOrDefault(a => a.IsActive) ?? Adapters.FirstOrDefault())?.Description
            ?? "未检测到网卡";
        HasAdapters = Adapters.Count > 0;
    }
}

public class NetworkItemViewModel : BaseViewModel
{
    public string AdapterName { get; set; } = string.Empty;

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private double _downloadSpeedMbps;
    public double DownloadSpeedMbps
    {
        get => _downloadSpeedMbps;
        set
        {
            if (SetProperty(ref _downloadSpeedMbps, value))
                OnPropertyChanged(nameof(DownloadAmountText));
        }
    }

    private double _uploadSpeedMbps;
    public double UploadSpeedMbps
    {
        get => _uploadSpeedMbps;
        set
        {
            if (SetProperty(ref _uploadSpeedMbps, value))
                OnPropertyChanged(nameof(UploadAmountText));
        }
    }

    public string DownloadAmountText => FormatAmount(DownloadSpeedMbps);
    public string UploadAmountText => FormatAmount(UploadSpeedMbps);

    private string _status = string.Empty;
    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
                OnPropertyChanged(nameof(StatusDisplay));
        }
    }

    public string StatusDisplay => Status switch
    {
        "Up" => "已连接",
        "Down" => "未连接",
        "Dormant" => "休眠",
        "NotPresent" => "不存在",
        "LowerLayerDown" => "链路断开",
        "Testing" => "正在测试",
        "Unknown" => "未知",
        _ => string.IsNullOrWhiteSpace(Status) ? "未知" : Status,
    };

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    private string _ipAddresses = string.Empty;
    public string IpAddresses
    {
        get => _ipAddresses;
        set => SetProperty(ref _ipAddresses, value);
    }

    private string _macAddress = "—";
    public string MacAddress
    {
        get => _macAddress;
        set => SetProperty(ref _macAddress, value);
    }

    public void Update(Models.NetworkInfo net)
    {
        AdapterName = net.AdapterName;
        Description = net.Description;
        DownloadSpeedMbps = net.DownloadSpeedMbps;
        UploadSpeedMbps = net.UploadSpeedMbps;
        Status = net.Status;
        IsActive = net.IsActive;
        IpAddresses = net.IpAddresses.Count > 0 ? string.Join(" · ", net.IpAddresses) : "无 IP 地址";
        MacAddress = string.IsNullOrWhiteSpace(net.MacAddress) ? "—" : net.MacAddress;
    }

    private static string FormatAmount(double mbps)
    {
        var (value, unit) = HardwareInfoService.FormatSpeedParts(mbps);
        return $"{value} {unit}";
    }
}
