using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinUIApp.Models;
using WinUIApp.Services;

namespace WinUIApp.ViewModels;

/// <summary>
/// 进程页状态：完整快照、筛选、排序与选中进程详情。
/// 集合按 PID 复用实例并就地移动，避免每次采样重建全部行。
/// </summary>
public sealed class ProcessesViewModel : BaseViewModel
{
    private IReadOnlyList<ProcessInfo> _snapshot = Array.Empty<ProcessInfo>();
    private long _totalWorkingSetBytes;
    private string _searchText = string.Empty;
    private int _sortMode;
    private int _slowIntervalSeconds = 5;
    private string _visibleSummary = "等待首次采样";
    private string _copyStatusText = string.Empty;
    private ProcessItemViewModel? _selectedItem;
    private int _detailsGeneration;

    private string _detailsPath = "—";
    private string _detailsStartText = "—";
    private string _detailsPriorityText = "—";

    public ObservableCollection<ProcessItemViewModel> Items { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                Rebuild();
        }
    }

    public void SetSearchText(string text)
    {
        if (text != _searchText)
            SearchText = text;
    }

    public int SortMode
    {
        get => _sortMode;
        set
        {
            if (SetProperty(ref _sortMode, value))
                Rebuild();
        }
    }

    public string VisibleSummary
    {
        get => _visibleSummary;
        private set => SetProperty(ref _visibleSummary, value);
    }

    public string CopyStatusText
    {
        get => _copyStatusText;
        private set => SetProperty(ref _copyStatusText, value);
    }

    public void SetCopyStatus(string text) => CopyStatusText = text;

    public ProcessItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!SetProperty(ref _selectedItem, value))
                return;

            OnPropertyChanged(nameof(SelectedTitle));
            OnPropertyChanged(nameof(SelectedPidText));
            OnPropertyChanged(nameof(DetailsCpuText));
            OnPropertyChanged(nameof(DetailsMemoryText));
            OnPropertyChanged(nameof(DetailsThreadsHandlesText));

            _ = LoadDetailsAsync(value);
        }
    }

    public string SelectedTitle => _selectedItem?.Name ?? "未选择进程";
    public string SelectedPidText => _selectedItem is null ? "PID —" : $"PID {_selectedItem.Id}";
    public string DetailsCpuText => _selectedItem?.CpuText ?? "—";
    public string DetailsThreadsHandlesText =>
        _selectedItem is null ? "—" : $"{_selectedItem.ThreadsText} / {_selectedItem.HandlesText}";

    public string DetailsMemoryText
    {
        get
        {
            if (_selectedItem is null)
                return "—";
            double share = _totalWorkingSetBytes > 0
                ? _selectedItem.MemoryBytes * 100.0 / _totalWorkingSetBytes
                : 0;
            return $"{_selectedItem.MemoryText} · 占 {share:0.0}%";
        }
    }

    public string DetailsPath
    {
        get => _detailsPath;
        private set => SetProperty(ref _detailsPath, value);
    }

    public string DetailsStartText
    {
        get => _detailsStartText;
        private set => SetProperty(ref _detailsStartText, value);
    }

    public string DetailsPriorityText
    {
        get => _detailsPriorityText;
        private set => SetProperty(ref _detailsPriorityText, value);
    }

    public string Subtitle => _snapshot.Count == 0
        ? $"每 {_slowIntervalSeconds} 秒更新进程与磁盘信息"
        : $"{_snapshot.Count} 个进程 · 合计 {ProcessInfo.FormatBytes(_totalWorkingSetBytes)} · 每 {_slowIntervalSeconds} 秒更新";

    public void SetSlowInterval(int seconds)
    {
        if (_slowIntervalSeconds == seconds)
            return;

        _slowIntervalSeconds = seconds;
        OnPropertyChanged(nameof(Subtitle));
    }

    public void Apply(ProcessSnapshot snapshot)
    {
        _snapshot = snapshot.Processes;
        _totalWorkingSetBytes = snapshot.TotalWorkingSetBytes;
        CopyStatusText = string.Empty;
        Rebuild();
    }

    private void Rebuild()
    {
        string search = _searchText.Trim();

        IEnumerable<ProcessInfo> query = _snapshot;
        if (search.Length > 0)
            query = query.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        query = _sortMode switch
        {
            1 => query.OrderByDescending(p => p.CpuPercent).ThenByDescending(p => p.WorkingSetBytes),
            2 => query.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase),
            3 => query.OrderBy(p => p.Id),
            _ => query.OrderByDescending(p => p.WorkingSetBytes).ThenBy(p => p.Id),
        };

        var desired = query.ToList();
        Reconcile(desired);

        VisibleSummary = search.Length > 0
            ? $"筛选 {Items.Count} / {_snapshot.Count} 个进程"
            : _snapshot.Count > 0 ? $"{_snapshot.Count} 个进程" : "等待首次采样";

        if (_selectedItem != null && !Items.Contains(_selectedItem))
            SelectedItem = null;
        else
            OnPropertyChanged(nameof(DetailsMemoryText));

        OnPropertyChanged(nameof(Subtitle));
    }

    private void Reconcile(IReadOnlyList<ProcessInfo> desired)
    {
        var existing = new Dictionary<int, ProcessItemViewModel>(Items.Count);
        foreach (var item in Items)
            existing[item.Id] = item;

        var desiredIds = new HashSet<int>(desired.Select(p => p.Id));
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (!desiredIds.Contains(Items[i].Id))
                Items.RemoveAt(i);
        }

        for (int i = 0; i < desired.Count; i++)
        {
            var info = desired[i];
            if (existing.TryGetValue(info.Id, out var item))
            {
                item.Update(info);
                int currentIndex = Items.IndexOf(item);
                if (currentIndex >= 0 && currentIndex != i)
                    Items.Move(currentIndex, i);
            }
            else
            {
                Items.Insert(i, new ProcessItemViewModel(info));
            }
        }

        long max = 0;
        foreach (var process in desired)
        {
            if (process.WorkingSetBytes > max)
                max = process.WorkingSetBytes;
        }

        foreach (var item in Items)
            item.SetMemoryShare(max);
    }

    private async Task LoadDetailsAsync(ProcessItemViewModel? item)
    {
        int generation = ++_detailsGeneration;
        DetailsPath = item is null ? "—" : "正在读取…";
        DetailsStartText = "—";
        DetailsPriorityText = "—";

        if (item is null)
            return;

        int id = item.Id;
        var details = await Task.Run(() => ProcessService.GetDetails(id));

        if (generation != _detailsGeneration || SelectedItem?.Id != id)
            return;

        DetailsPath = details.Path;
        DetailsStartText = details.StartTime;
        DetailsPriorityText = details.Priority;
    }

    public string CreateListText()
    {
        var text = new StringBuilder();
        text.AppendLine($"本机进程 · {DateTime.Now:yyyy-MM-dd HH:mm:ss} · {Items.Count} 个进程");
        text.AppendLine("名称\tPID\tCPU\t内存\t线程\t句柄");
        foreach (var item in Items)
            text.AppendLine($"{item.Name}\t{item.Id}\t{item.CpuText}\t{item.MemoryText}\t{item.ThreadsText}\t{item.HandlesText}");
        return text.ToString().TrimEnd();
    }
}

public sealed class ProcessItemViewModel : BaseViewModel
{
    public ProcessItemViewModel(ProcessInfo info)
    {
        Id = info.Id;
        _name = info.Name;
        _workingSetBytes = info.WorkingSetBytes;
        _cpuPercent = info.CpuPercent;
        _threadCount = info.ThreadCount;
        _handleCount = info.HandleCount;
    }

    public int Id { get; }

    private string _name;
    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value, nameof(Name));
    }

    private long _workingSetBytes;
    public long MemoryBytes
    {
        get => _workingSetBytes;
        private set
        {
            if (SetProperty(ref _workingSetBytes, value, nameof(MemoryBytes)))
                OnPropertyChanged(nameof(MemoryText));
        }
    }

    public string MemoryText => ProcessInfo.FormatBytes(_workingSetBytes);

    private double _cpuPercent;
    public double CpuPercent
    {
        get => _cpuPercent;
        private set
        {
            if (SetProperty(ref _cpuPercent, value, nameof(CpuPercent)))
                OnPropertyChanged(nameof(CpuText));
        }
    }

    public string CpuText => $"{_cpuPercent:0.0}%";

    private int _threadCount;
    public int ThreadCount
    {
        get => _threadCount;
        private set
        {
            if (SetProperty(ref _threadCount, value, nameof(ThreadCount)))
                OnPropertyChanged(nameof(ThreadsText));
        }
    }

    public string ThreadsText => _threadCount.ToString();

    private int _handleCount;
    public int HandleCount
    {
        get => _handleCount;
        private set
        {
            if (SetProperty(ref _handleCount, value, nameof(HandleCount)))
                OnPropertyChanged(nameof(HandlesText));
        }
    }

    public string HandlesText => _handleCount.ToString();

    private double _memorySharePercent;
    public double MemorySharePercent
    {
        get => _memorySharePercent;
        private set => SetProperty(ref _memorySharePercent, value, nameof(MemorySharePercent));
    }

    public void Update(ProcessInfo info)
    {
        Name = info.Name;
        MemoryBytes = info.WorkingSetBytes;
        CpuPercent = info.CpuPercent;
        ThreadCount = info.ThreadCount;
        HandleCount = info.HandleCount;
    }

    public void SetMemoryShare(long maxWorkingSetBytes)
    {
        MemorySharePercent = maxWorkingSetBytes > 0
            ? _workingSetBytes * 100.0 / maxWorkingSetBytes
            : 0;
    }
}
