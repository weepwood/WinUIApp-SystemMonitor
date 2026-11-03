using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using WinUIApp.Models;

namespace WinUIApp.Services;

/// <summary>
/// Reads cumulative byte counters via the managed network API. No WMI round-trip.
/// Rates are decimal megabits per second (1 Mbps = 1,000,000 bits/s).
/// </summary>
public sealed class NetworkService
{
    private readonly Dictionary<string, (long received, long sent, long timestamp)> _previousStats = new();

    public List<NetworkInfo> GetNetworkInfos()
    {
        var networks = new List<NetworkInfo>();
        var now = Stopwatch.GetTimestamp();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        NetworkInterface[] adapters;
        try { adapters = NetworkInterface.GetAllNetworkInterfaces(); }
        catch (NetworkInformationException) { return networks; }

        foreach (var nic in adapters)
        {
            try
            {
                var info = new NetworkInfo
                {
                    AdapterName = nic.Id,
                    Description = string.IsNullOrWhiteSpace(nic.Description) ? nic.Name : nic.Description,
                    Status = nic.OperationalStatus.ToString(),
                    IsActive = nic.OperationalStatus == OperationalStatus.Up
                };
                seen.Add(nic.Id);

                var props = nic.GetIPProperties();
                foreach (var address in props.UnicastAddresses)
                {
                    if (address.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                        info.IpAddresses.Add(address.Address.ToString());
                }

                byte[] mac = nic.GetPhysicalAddress().GetAddressBytes();
                info.MacAddress = string.Join(":", mac.Select(b => b.ToString("X2")));

                var stats = nic.GetIPv4Statistics();
                info.BytesReceived = stats.BytesReceived;
                info.BytesSent = stats.BytesSent;

                if (_previousStats.TryGetValue(nic.Id, out var previous))
                {
                    double elapsed = (now - previous.timestamp) / (double)Stopwatch.Frequency;
                    if (elapsed > 0.001)
                    {
                        long receivedDelta = info.BytesReceived - previous.received;
                        long sentDelta = info.BytesSent - previous.sent;
                        // Counter reset / device replacement must not produce a large spike.
                        if (receivedDelta >= 0 && sentDelta >= 0)
                        {
                            info.DownloadSpeedMbps = Math.Round(receivedDelta * 8.0 / 1_000_000.0 / elapsed, 2);
                            info.UploadSpeedMbps = Math.Round(sentDelta * 8.0 / 1_000_000.0 / elapsed, 2);
                        }
                    }
                }

                _previousStats[nic.Id] = (info.BytesReceived, info.BytesSent, now);
                networks.Add(info);
            }
            catch (Exception)
            {
                // An interface may disappear while it is being enumerated.
            }
        }

        foreach (string id in _previousStats.Keys.Where(id => !seen.Contains(id)).ToArray())
            _previousStats.Remove(id);

        return networks;
    }
}
