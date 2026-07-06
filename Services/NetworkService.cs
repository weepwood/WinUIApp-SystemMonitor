using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using WinUIApp.Models;

namespace WinUIApp.Services;

/// <summary>
/// 网络适配器信息和流量采集服务
/// </summary>
public class NetworkService
{
    private readonly ConcurrentDictionary<string, (long received, long sent, DateTime time)> _previousStats = new();

    public List<NetworkInfo> GetNetworkInfos()
    {
        var networks = new List<NetworkInfo>();
        var now = DateTime.UtcNow;

        try
        {
            var results = HardwareInfoService.ExecuteQuery(
                "SELECT * FROM Win32_PerfRawData_Tcpip_NetworkInterface");

            foreach (var obj in results)
            {
                string name = HardwareInfoService.GetStringProperty(obj, "Name");
                long bytesReceived = (long)(HardwareInfoService.GetPropertyValue<ulong>(obj, "BytesReceivedPerSec") ?? 0);
                long bytesSent = (long)(HardwareInfoService.GetPropertyValue<ulong>(obj, "BytesSentPerSec") ?? 0);
                long totalReceived = (long)(HardwareInfoService.GetPropertyValue<ulong>(obj, "BytesTotalPersec") ?? 0);

                var netInfo = new NetworkInfo
                {
                    AdapterName = name,
                    Description = HardwareInfoService.GetStringProperty(obj, "Description"),
                    BytesReceived = bytesReceived,
                    BytesSent = bytesSent,
                    IsActive = bytesReceived > 0 || bytesSent > 0,
                };

                // 用原始计数器计算速率
                try
                {
                    ulong rawReceived = HardwareInfoService.GetPropertyValue<ulong>(obj, "BytesReceivedPersec") ?? 0;
                    ulong rawSent = HardwareInfoService.GetPropertyValue<ulong>(obj, "BytesSentPersec") ?? 0;

                    if (_previousStats.TryGetValue(name, out var prev))
                    {
                        double elapsedSec = (now - prev.time).TotalSeconds;
                        if (elapsedSec > 0)
                        {
                            long deltaReceived = Math.Max(0, (long)rawReceived - prev.received);
                            long deltaSent = Math.Max(0, (long)rawSent - prev.sent);

                            netInfo.DownloadSpeedMbps = HardwareInfoService.BytesToMB(deltaReceived) * 8 / elapsedSec;
                            netInfo.UploadSpeedMbps = HardwareInfoService.BytesToMB(deltaSent) * 8 / elapsedSec;
                        }
                    }
                    _previousStats[name] = ((long)rawReceived, (long)rawSent, now);
                }
                catch (Exception) { }

                networks.Add(netInfo);
            }
        }
        catch (Exception) { }

        // 如果 WMI 获取不到网络接口，使用 System.Net.NetworkInformation
        if (networks.Count == 0)
        {
            networks = GetNetworkInfoFromApi();
        }
        else
        {
            // 从 System.Net.NetworkInformation 获取附加信息（IP、MAC、状态等）
            EnrichNetworkInfo(networks);
        }

        return networks;
    }

    private static List<NetworkInfo> GetNetworkInfoFromApi()
    {
        var networks = new List<NetworkInfo>();

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                var stats = nic.GetIPStatistics();
                var ipProps = nic.GetIPProperties();

                var netInfo = new NetworkInfo
                {
                    AdapterName = nic.Name,
                    Description = nic.Description,
                    Status = nic.OperationalStatus.ToString(),
                    IsActive = nic.OperationalStatus == OperationalStatus.Up,
                };

                foreach (var ip in ipProps.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork ||
                        ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        netInfo.IpAddresses.Add(ip.Address.ToString());
                    }
                }

                var mac = nic.GetPhysicalAddress();
                if (mac != null)
                {
                    netInfo.MacAddress = string.Join(":", mac.GetAddressBytes().Select(b => b.ToString("X2")));
                }

                networks.Add(netInfo);
            }
        }
        catch (Exception) { }

        return networks;
    }

    private static void EnrichNetworkInfo(List<NetworkInfo> networks)
    {
        try
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var nic in nics)
            {
                var match = networks.FirstOrDefault(n =>
                    n.Description.Contains(nic.Name, StringComparison.OrdinalIgnoreCase) ||
                    n.AdapterName.Contains(nic.Name, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                    match = networks.FirstOrDefault(n =>
                        n.Description.Contains(nic.Description, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    match.Status = nic.OperationalStatus.ToString();
                    match.IsActive = match.IsActive || nic.OperationalStatus == OperationalStatus.Up;

                    var ipProps = nic.GetIPProperties();
                    foreach (var ip in ipProps.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork ||
                            ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            if (!match.IpAddresses.Contains(ip.Address.ToString()))
                                match.IpAddresses.Add(ip.Address.ToString());
                        }
                    }

                    var mac = nic.GetPhysicalAddress();
                    if (mac != null && string.IsNullOrEmpty(match.MacAddress))
                    {
                        match.MacAddress = string.Join(":", mac.GetAddressBytes().Select(b => b.ToString("X2")));
                    }
                }
            }
        }
        catch (Exception) { }
    }
}
