using System.Collections.ObjectModel;
using System.Linq;
using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>
/// 网络信息 ViewModel
/// </summary>
public class NetworkViewModel : BaseViewModel
{
    private readonly NetworkService _service = new();

    public ObservableCollection<NetworkItemViewModel> Adapters { get; } = new();

    public void Refresh()
    {
        var networks = _service.GetNetworkInfos();
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
        set => SetProperty(ref _downloadSpeedMbps, value);
    }

    private double _uploadSpeedMbps;
    public double UploadSpeedMbps
    {
        get => _uploadSpeedMbps;
        set => SetProperty(ref _uploadSpeedMbps, value);
    }

    private string _downloadSpeedText = "0 Mbps";
    public string DownloadSpeedText
    {
        get => _downloadSpeedText;
        set => SetProperty(ref _downloadSpeedText, value);
    }

    private string _uploadSpeedText = "0 Mbps";
    public string UploadSpeedText
    {
        get => _uploadSpeedText;
        set => SetProperty(ref _uploadSpeedText, value);
    }

    private string _status = string.Empty;
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    private string _ipAddresses = string.Empty;
    public string IpAddresses
    {
        get => _ipAddresses;
        set => SetProperty(ref _ipAddresses, value);
    }

    public void Update(Models.NetworkInfo net)
    {
        AdapterName = net.AdapterName;
        Description = net.Description;
        DownloadSpeedMbps = net.DownloadSpeedMbps;
        UploadSpeedMbps = net.UploadSpeedMbps;
        Status = net.Status;
        IpAddresses = net.IpAddresses.Count > 0 ? string.Join(", ", net.IpAddresses) : "无 IP";

        DownloadSpeedText = HardwareInfoService.FormatSpeed(net.DownloadSpeedMbps);
        UploadSpeedText = HardwareInfoService.FormatSpeed(net.UploadSpeedMbps);
    }
}
