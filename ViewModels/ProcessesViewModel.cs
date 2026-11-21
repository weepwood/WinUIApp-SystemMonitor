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
/// 集合按 PID 复用实例；内存 / CPU 排序使用带滞回的「惰性排序」，
/// 只有明显超过相邻项时才换位，避免列表每秒跳动。
/// </summary>
public sealed class ProcessesViewModel : BaseViewModel
{
    private const double MemorySwapMargin = 1.15;   // 大出 15% 才上移
    private const double CpuSwapMargin = 1.5;       // 高出 1.5 个百分点才上移

    private IReadOnlyList<ProcessInfo> _snapshot = Array.Empty<ProcessInfo>();
    private long _totalWorkingSetBytes;
    private string _searchText = string.Empty;
    private int _sortMode;
    private bool _sortDescending = true;
    private int _slowIntervalSeconds = 5;
    private string _visibleSummary = "等待首次采样";
    private string _actionStatusText = string.Empty;
    private ProcessItemViewModel? _selectedItem;
    private int _detailsGeneration;
    private bool _forceReorder;

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
            {
                _forceReorder = true;
                Rebuild();
            }
        }
    }

    public void SetSearchText(string text)
    {
        if (text != _searchText)
            SearchText = text;
    }

    /// <summary>0 内存、1 CPU、2 名称、3 PID。</summary>
    public int SortMode
    {
        get => _sortMode;
        set
        {
            if (SetProperty(ref _sortMode, value))
            {
                _forceReorder = true;
                Rebuild();
            }
        }
    }

    public bool SortDescending
    {
        get => _sortDescending;
        set
        {
            if (SetProperty(ref _sortDescending, value))
            {
                _forceReorder = true;
                Rebuild();
            }
        }
    }

    public string VisibleSummary
    {
        get => _visibleSummary;
        private set => SetProperty(ref _visibleSummary, value);
    }

    public string ActionStatusText
    {
        get => _actionStatusText;
        private set => SetProperty(ref _actionStatusText, value);
    }

    public void NotifyActionStatus(string text) => ActionStatusText = text;

    public ProcessItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!SetProperty(ref _selectedItem, value))
                return;

            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedTitle));
            OnPropertyChanged(nameof(SelectedPidText));
            OnPropertyChanged(nameof(DetailsCpuText));
            OnPropertyChanged(nameof(DetailsMemoryText));
            OnPropertyChanged(nameof(DetailsThreadsHandlesText));

            _ = LoadDetailsAsync(value);
        }
    }

    public bool HasSelection => _selectedItem != null;

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

    /// <summary>详情里的路径是否可用于「打开位置 / 复制路径」。</summary>
    public bool TryGetSelectedPath(out string path)
    {
        path = _detailsPath;
        return _selectedItem != null
            && !string.IsNullOrWhiteSpace(path)
            && path != "—"
            && path != "正在读取…"
            && path != "不可访问"
            && path != "进程已退出";
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
        ActionStatusText = string.Empty;
        Rebuild();
    }

    private bool UsesLazyOrder => _sortMode is 0 or 1;

    private void Rebuild()
    {
        string search = _searchText.Trim();

        IEnumerable<ProcessInfo> query = _snapshot;
        if (search.Length > 0)
            query = query.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        var filtered = query.ToList();
        var index = new Dictionary<int, ProcessInfo>(filtered.Count);
        foreach (var process in filtered)
            index[process.Id] = process;

        // 1. 保留仍然存在（且通过筛选）的行，同时刷新数值。
        var ordered = new List<ProcessItemViewModel>(filtered.Count);
        var present = new HashSet<int>(Items.Count);
        foreach (var item in Items)
        {
            if (!index.TryGetValue(item.Id, out var info))
                continue;

            item.Update(info);
            ordered.Add(item);
            present.Add(item.Id);
        }

        // 2. 新出现的进程按精确顺序插入到大致位置，避免从末尾慢慢爬升。
        foreach (var process in filtered)
        {
            if (present.Contains(process.Id))
                continue;

            var item = new ProcessItemViewModel(process);
            ordered.Insert(FindInsertIndex(ordered, item), item);
        }

        // 3. 内存 / CPU 用带滞回的插入排序，其余模式（以及切换排序的那一刻）精确排序。
        if (UsesLazyOrder && !_forceReorder)
            StabilizeOrder(ordered);
        else
            ordered.Sort((a, b) => Compare(a, b));

        _forceReorder = false;

        SyncItems(ordered);

        long max = 0;
        foreach (var process in filtered)
        {
            if (process.WorkingSetBytes > max)
                max = process.WorkingSetBytes;
        }

        foreach (var item in Items)
            item.SetMemoryShare(max);

        VisibleSummary = search.Length > 0
            ? $"筛选 {Items.Count} / {_snapshot.Count} 个进程"
            : _snapshot.Count > 0 ? $"{_snapshot.Count} 个进程" : "等待首次采样";

        if (_selectedItem != null && !Items.Contains(_selectedItem))
            SelectedItem = null;
        else
            OnPropertyChanged(nameof(DetailsMemoryText));

        OnPropertyChanged(nameof(Subtitle));
    }

    /// <summary>比较：仅在「升序」语义下返回负值表示 a 排在 b 之前。</summary>
    private int Compare(ProcessItemViewModel a, ProcessItemViewModel b)
    {
        int result = _sortMode switch
        {
            1 => a.CpuPercent.CompareTo(b.CpuPercent),
            2 => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase),
            3 => a.Id.CompareTo(b.Id),
            _ => a.MemoryBytes.CompareTo(b.MemoryBytes),
        };

        return _sortDescending ? -result : result;
    }

    private int FindInsertIndex(List<ProcessItemViewModel> ordered, ProcessItemViewModel item)
    {
        int low = 0;
        int high = ordered.Count;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (Compare(item, ordered[mid]) < 0)
                high = mid;
            else
                low = mid + 1;
        }

        return low;
    }

    /// <summary>
    /// 带滞回的插入排序：只有当某一项按排序键明显优于前一项时才换位，
    /// 因此相邻项的轻微波动不会造成行跳动，而新进程仍能一步到位。
    /// </summary>
    private void StabilizeOrder(List<ProcessItemViewModel> ordered)
    {
        for (int i = 1; i < ordered.Count; i++)
        {
            var item = ordered[i];
            int j = i - 1;
            while (j >= 0 && ShouldMoveAhead(item, ordered[j]))
            {
                ordered[j + 1] = ordered[j];
                j--;
            }

            ordered[j + 1] = item;
        }
    }

    private bool ShouldMoveAhead(ProcessItemViewModel item, ProcessItemViewModel other)
    {
        if (_sortMode == 0)
        {
            return _sortDescending
                ? item.MemoryBytes > other.MemoryBytes * MemorySwapMargin
                : item.MemoryBytes * MemorySwapMargin < other.MemoryBytes;
        }

        if (_sortMode == 1)
        {
            return _sortDescending
                ? item.CpuPercent > other.CpuPercent + CpuSwapMargin
                : item.CpuPercent + CpuSwapMargin < other.CpuPercent;
        }

        return Compare(item, other) < 0;
    }

    private void SyncItems(List<ProcessItemViewModel> ordered)
    {
        var keep = new HashSet<ProcessItemViewModel>(ordered);

        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(Items[i]))
                Items.RemoveAt(i);
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            int current = Items.IndexOf(item);

            if (current < 0)
                Items.Insert(i, item);
            else if (current != i)
                Items.Move(current, i);
        }
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
