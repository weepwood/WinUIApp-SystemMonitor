using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WinUIApp.Models;

namespace WinUIApp.Services;

public static class ProcessService
{
    // One enumeration supplies both the totals and the ranking. A process may exit
    // or deny access between any two reads; don't discard the rest of the sample.
    public static void Sample(SystemInfo info, int topCount = 5)
    {
        var rows = new List<ProcessMemoryInfo>();
        var processes = Process.GetProcesses();
        info.ProcessCount = processes.Length;
        foreach (var process in processes)
        {
            using (process)
            {
                try { info.ThreadCount += process.Threads.Count; } catch (Exception) { }
                try { info.HandleCount += process.HandleCount; } catch (Exception) { }
                try { rows.Add(new ProcessMemoryInfo(process.ProcessName, process.Id, process.WorkingSet64)); }
                catch (Exception) { }
            }
        }
        int count = Math.Clamp(topCount, 1, 30);
        info.TopProcesses = rows.OrderByDescending(p => p.WorkingSetBytes).ThenBy(p => p.Id).Take(count).ToArray();
    }
}
