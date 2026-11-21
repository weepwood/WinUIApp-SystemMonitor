using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinUIApp.Services;

/// <summary>
/// 本机监测偏好。所有取值在加载与保存时都会归一化，
/// 因此手写或旧版本的配置文件不会让界面进入无效状态。
/// </summary>
public sealed class MonitorPreferences
{
    public const int CurrentSchemaVersion = 4;

    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinUIApp-SystemMonitor");

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>System / Light / Dark</summary>
    public string Theme { get; set; } = "System";

    /// <summary>Mica / Acrylic / Solid</summary>
    public string Backdrop { get; set; } = "Mica";

    public bool AlwaysOnTop { get; set; }

    public bool StartMaximized { get; set; }

    public bool RememberWindowSize { get; set; } = true;

    public int WindowWidth { get; set; } = 1180;

    public int WindowHeight { get; set; } = 800;

    /// <summary>采样间隔，单位秒：1 / 2 / 5 / 10。</summary>
    public int IntervalSeconds { get; set; } = 1;

    public bool PauseWhenMinimized { get; set; } = true;

    /// <summary>趋势图保留的采样点数量：30 / 60 / 120。</summary>
    public int HistoryCapacity { get; set; } = 60;

    public bool ShowChartGrid { get; set; } = true;

    public bool ShowChartArea { get; set; } = true;

    public bool ShowThresholdLine { get; set; }

    public bool CpuAlertEnabled { get; set; }

    public double CpuAlertThreshold { get; set; } = 90;

    public bool MemoryAlertEnabled { get; set; }

    public double MemoryAlertThreshold { get; set; } = 90;

    /// <summary>系统信息页显示的进程数量：5 / 10 / 15。</summary>
    public int TopProcessCount { get; set; } = 5;

    public bool StartWithWindows { get; set; }

    /// <summary>进程页与磁盘、系统信息的刷新间隔，单位秒：2 / 5 / 10。</summary>
    public int ProcessRefreshSeconds { get; set; } = 5;

    /// <summary>进程页排序方式：0 内存、1 CPU、2 名称、3 PID。</summary>
    public int ProcessSortMode { get; set; }

    /// <summary>进程页排序方向：true 为降序（大 → 小 / Z → A）。</summary>
    public bool ProcessSortDescending { get; set; } = true;

    /// <summary>进程页的搜索关键字，重启后保留。</summary>
    public string ProcessFilterText { get; set; } = string.Empty;

    /// <summary>趋势图是否显示鼠标悬停读数。</summary>
    public bool ShowChartTooltip { get; set; } = true;

    [JsonIgnore]
    public static string ConfigDirectory => FolderPath;

    [JsonIgnore]
    public static string ConfigFilePath => Path.Combine(FolderPath, "preferences.json");

    public static MonitorPreferences CreateDefault()
    {
        var preferences = new MonitorPreferences();
        preferences.Normalize();
        return preferences;
    }

    public static MonitorPreferences Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
                return CreateDefault();

            var preferences = JsonSerializer.Deserialize<MonitorPreferences>(File.ReadAllText(ConfigFilePath));
            if (preferences is null)
                return CreateDefault();

            preferences.Normalize();
            return preferences;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return CreateDefault();
        }
    }

    /// <summary>把每个字段限制在界面支持的取值范围内。</summary>
    public void Normalize()
    {
        SchemaVersion = CurrentSchemaVersion;

        Theme = Theme is "Light" or "Dark" ? Theme : "System";
        Backdrop = Backdrop is "Acrylic" or "Solid" ? Backdrop : "Mica";
        IntervalSeconds = IntervalSeconds is 1 or 2 or 5 or 10 ? IntervalSeconds : 1;
        HistoryCapacity = HistoryCapacity is 30 or 60 or 120 ? HistoryCapacity : 60;
        TopProcessCount = TopProcessCount is 10 or 15 ? TopProcessCount : 5;
        ProcessRefreshSeconds = ProcessRefreshSeconds is 2 or 10 ? ProcessRefreshSeconds : 5;
        ProcessSortMode = Math.Clamp(ProcessSortMode, 0, 3);
        ProcessFilterText = (ProcessFilterText ?? string.Empty).Trim();
        if (ProcessFilterText.Length > 64)
            ProcessFilterText = ProcessFilterText[..64];

        WindowWidth = Math.Clamp(WindowWidth, 760, 7680);
        WindowHeight = Math.Clamp(WindowHeight, 520, 4320);

        CpuAlertThreshold = Math.Clamp(Math.Round(CpuAlertThreshold), 50, 99);
        MemoryAlertThreshold = Math.Clamp(Math.Round(MemoryAlertThreshold), 50, 99);
    }

    public bool Save()
    {
        try
        {
            Normalize();
            Directory.CreateDirectory(FolderPath);
            string tempPath = ConfigFilePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(tempPath, ConfigFilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };
}
