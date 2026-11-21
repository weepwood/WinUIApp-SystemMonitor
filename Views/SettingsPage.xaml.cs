using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.ApplicationModel.DataTransfer;
using WinUIApp.Services;

namespace WinUIApp.Views;

public sealed partial class SettingsPage : Page
{
    public event Action<MonitorPreferences>? PreferencesChanged;

    private MonitorPreferences? _preferences;
    private bool _isLoading;

    public SettingsPage()
    {
        InitializeComponent();
        UpdateAboutInfo();
    }

    public void Initialize(MonitorPreferences preferences)
    {
        _isLoading = true;
        _preferences = preferences;

        SelectTag(ThemeSelector, preferences.Theme);
        SelectTag(BackdropSelector, preferences.Backdrop);
        SelectTag(IntervalSelector, preferences.IntervalSeconds.ToString());
        SelectTag(CapacitySelector, preferences.HistoryCapacity.ToString());
        SelectTag(ProcessCountSelector, preferences.TopProcessCount.ToString());

        PinSwitch.IsOn = preferences.AlwaysOnTop;
        MaximizeSwitch.IsOn = preferences.StartMaximized;
        RememberSizeSwitch.IsOn = preferences.RememberWindowSize;
        PauseMinimizedSwitch.IsOn = preferences.PauseWhenMinimized;
        GridSwitch.IsOn = preferences.ShowChartGrid;
        AreaSwitch.IsOn = preferences.ShowChartArea;
        ThresholdLineSwitch.IsOn = preferences.ShowThresholdLine;

        CpuAlertSwitch.IsOn = preferences.CpuAlertEnabled;
        MemoryAlertSwitch.IsOn = preferences.MemoryAlertEnabled;
        CpuThresholdSlider.Value = preferences.CpuAlertThreshold;
        MemoryThresholdSlider.Value = preferences.MemoryAlertThreshold;

        // 开机启动项以注册表的实际状态为准。
        StartupSwitch.IsOn = StartupRegistration.IsEnabled();
        preferences.StartWithWindows = StartupSwitch.IsOn;

        ConfigCard.Description = MonitorPreferences.ConfigFilePath;

        _isLoading = false;

        UpdateThresholdLabels();
        UpdateThresholdCardVisibility();
        UpdateAboutInfo();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreferences();

    private void OnSettingToggled(object sender, RoutedEventArgs e) => UpdatePreferences();

    private void OnAlertToggled(object sender, RoutedEventArgs e)
    {
        UpdateThresholdCardVisibility();
        UpdatePreferences();
    }

    private void OnThresholdChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (CpuThresholdText is null || MemoryThresholdText is null)
            return;

        UpdateThresholdLabels();
        if (!_isLoading)
            UpdatePreferences();
    }

    private void UpdateThresholdLabels()
    {
        CpuThresholdText.Text = $"{CpuThresholdSlider.Value:0}%";
        MemoryThresholdText.Text = $"{MemoryThresholdSlider.Value:0}%";
    }

    private void UpdateThresholdCardVisibility()
    {
        CpuThresholdCard.Visibility = CpuAlertSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        MemoryThresholdCard.Visibility = MemoryAlertSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdatePreferences()
    {
        if (_isLoading || _preferences is null)
            return;

        _preferences.Theme = GetTag(ThemeSelector, "System");
        _preferences.Backdrop = GetTag(BackdropSelector, "Mica");
        _preferences.IntervalSeconds = int.TryParse(GetTag(IntervalSelector, "1"), out int seconds) ? seconds : 1;
        _preferences.HistoryCapacity = int.TryParse(GetTag(CapacitySelector, "60"), out int capacity) ? capacity : 60;
        _preferences.TopProcessCount = int.TryParse(GetTag(ProcessCountSelector, "5"), out int processCount) ? processCount : 5;

        _preferences.AlwaysOnTop = PinSwitch.IsOn;
        _preferences.StartMaximized = MaximizeSwitch.IsOn;
        _preferences.RememberWindowSize = RememberSizeSwitch.IsOn;
        _preferences.PauseWhenMinimized = PauseMinimizedSwitch.IsOn;

        _preferences.ShowChartGrid = GridSwitch.IsOn;
        _preferences.ShowChartArea = AreaSwitch.IsOn;
        _preferences.ShowThresholdLine = ThresholdLineSwitch.IsOn;

        _preferences.CpuAlertEnabled = CpuAlertSwitch.IsOn;
        _preferences.CpuAlertThreshold = CpuThresholdSlider.Value;
        _preferences.MemoryAlertEnabled = MemoryAlertSwitch.IsOn;
        _preferences.MemoryAlertThreshold = MemoryThresholdSlider.Value;

        _preferences.StartWithWindows = StartupSwitch.IsOn;

        PreferencesChanged?.Invoke(_preferences);
        ReportSaveState(_preferences.Save());
    }

    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _preferences is null)
            return;

        bool enable = StartupSwitch.IsOn;
        if (!StartupRegistration.TrySetEnabled(enable, out string? error))
        {
            StartupWarning.Message = error ?? "未知错误。";
            StartupWarning.IsOpen = true;

            _isLoading = true;
            StartupSwitch.IsOn = !enable;
            _isLoading = false;
            return;
        }

        StartupWarning.IsOpen = false;
        _preferences.StartWithWindows = enable;
        ReportSaveState(_preferences.Save());
    }

    private void ReportSaveState(bool saved)
    {
        StatusBar.Severity = InfoBarSeverity.Warning;
        StatusBar.Title = "当前更改已应用，但无法保存到本机。";
        StatusBar.IsOpen = !saved;
    }

    private void OnOpenConfigFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(MonitorPreferences.ConfigDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = MonitorPreferences.ConfigDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
            StatusBar.Severity = InfoBarSeverity.Warning;
            StatusBar.Title = "无法打开配置文件所在文件夹。";
            StatusBar.IsOpen = true;
        }
    }

    private void OnCopyConfigPath(object sender, RoutedEventArgs e)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(MonitorPreferences.ConfigFilePath);
            Clipboard.SetContent(package);

            StatusBar.Severity = InfoBarSeverity.Success;
            StatusBar.Title = "配置文件路径已复制到剪贴板。";
            StatusBar.IsOpen = true;
        }
        catch (Exception)
        {
            StatusBar.Severity = InfoBarSeverity.Warning;
            StatusBar.Title = "无法写入剪贴板，请重试。";
            StatusBar.IsOpen = true;
        }
    }

    private async void OnResetDefaults(object sender, RoutedEventArgs e)
    {
        if (_preferences is null)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "恢复默认设置？",
            Content = "外观、窗口、采样、趋势图与提醒选项将恢复为默认值，采集到的历史数据不受影响。",
            PrimaryButtonText = "恢复默认值",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var defaults = MonitorPreferences.CreateDefault();
        defaults.WindowWidth = _preferences.WindowWidth;
        defaults.WindowHeight = _preferences.WindowHeight;

        StartupRegistration.TrySetEnabled(false, out _);
        defaults.StartWithWindows = false;

        Initialize(defaults);
        PreferencesChanged?.Invoke(defaults);

        bool saved = defaults.Save();
        StatusBar.Severity = saved ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        StatusBar.Title = saved ? "已恢复默认设置。" : "已恢复默认设置，但无法保存到本机。";
        StatusBar.IsOpen = true;
    }

    private void UpdateAboutInfo()
    {
        if (AboutVersion is null || AboutRuntime is null)
            return;

        var version = typeof(App).Assembly.GetName().Version;
        AboutVersion.Value = version is null ? "1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        AboutRuntime.Value = RuntimeInformation.FrameworkDescription;
    }

    private static void SelectTag(ComboBox comboBox, string tag)
    {
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item && item.Tag is string itemTag && itemTag == tag)
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
            comboBox.SelectedIndex = 0;
    }

    private static string GetTag(ComboBox comboBox, string fallback) =>
        comboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag ? tag : fallback;
}
