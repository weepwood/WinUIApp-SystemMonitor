using System.Collections.Generic;

namespace WinUIApp.Models;

/// <summary>
/// 网络适配器信息和实时流量数据
/// </summary>
public class NetworkInfo
{
    public string AdapterName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double DownloadSpeedMbps { get; set; }
    public double UploadSpeedMbps { get; set; }
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }
    public List<string> IpAddresses { get; set; } = new();
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
