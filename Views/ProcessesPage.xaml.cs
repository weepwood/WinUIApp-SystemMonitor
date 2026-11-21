using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.ApplicationModel.DataTransfer;
using WinUIApp.ViewModels;

namespace WinUIApp.Views;

public sealed partial class ProcessesPage : Page
{
    /// <summary>排序或筛选变化时触发，由窗口把选择写入偏好。</summary>
    public event Action? ProcessViewChanged;

    private bool _isLoading;

    public ProcessesPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => ProcessViewChanged?.Invoke();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel monitor)
            return;

        _isLoading = true;
        SortSelector.SelectedIndex = Math.Clamp(monitor.Processes.SortMode, 0, 3);
        SortDirectionToggle.IsChecked = monitor.Processes.SortDescending;
        _isLoading = false;
        UpdateSortDirectionLabel();
    }

    private void OnSortSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || DataContext is not MainViewModel monitor)
            return;

        monitor.Processes.SortMode = Math.Max(0, SortSelector.SelectedIndex);
        ProcessViewChanged?.Invoke();
    }

    private void OnSortDirectionToggled(object sender, RoutedEventArgs e)
    {
        UpdateSortDirectionLabel();

        if (_isLoading || DataContext is not MainViewModel monitor)
            return;

        monitor.Processes.SortDescending = SortDirectionToggle.IsChecked == true;
        ProcessViewChanged?.Invoke();
    }

    private void UpdateSortDirectionLabel()
    {
        SortDirectionToggle.Content = SortDirectionToggle.IsChecked == true ? "降序" : "升序";
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (DataContext is MainViewModel monitor)
            monitor.Processes.SetSearchText(sender.Text);
    }

    private void OnSearchLostFocus(object sender, RoutedEventArgs e) => ProcessViewChanged?.Invoke();

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel monitor)
            monitor.RefreshNow();
    }

    private void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel monitor)
            return;

        try
        {
            var package = new DataPackage();
            package.SetText(monitor.Processes.CreateListText());
            Clipboard.SetContent(package);
            monitor.Processes.NotifyActionStatus("已复制列表");
        }
        catch (Exception)
        {
            monitor.Processes.NotifyActionStatus("无法写入剪贴板");
        }
    }

    private void OnCopyPathClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel monitor)
            return;

        if (!monitor.Processes.TryGetSelectedPath(out string path))
        {
            monitor.Processes.NotifyActionStatus("暂无可用的文件路径");
            return;
        }

        try
        {
            var package = new DataPackage();
            package.SetText(path);
            Clipboard.SetContent(package);
            monitor.Processes.NotifyActionStatus("路径已复制");
        }
        catch (Exception)
        {
            monitor.Processes.NotifyActionStatus("无法写入剪贴板");
        }
    }

    private void OnRevealClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel monitor)
            return;

        if (!monitor.Processes.TryGetSelectedPath(out string path))
        {
            monitor.Processes.NotifyActionStatus("暂无可用的文件路径");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
            monitor.Processes.NotifyActionStatus("已在资源管理器中定位");
        }
        catch (Exception)
        {
            monitor.Processes.NotifyActionStatus("无法打开资源管理器");
        }
    }

    private async void OnKillClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel monitor || monitor.Processes.SelectedItem is not { } item)
            return;

        if (item.Id == Environment.ProcessId)
        {
            monitor.Processes.NotifyActionStatus("不能结束监测进程本身");
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"结束 {item.Name}？",
            Content = $"PID {item.Id} 会被强制结束，未保存的数据将丢失。",
            PrimaryButtonText = "结束进程",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        try
        {
            using var process = Process.GetProcessById(item.Id);
            process.Kill();
            monitor.Processes.NotifyActionStatus("进程已结束");
            monitor.RefreshNow();
        }
        catch (Exception ex)
        {
            monitor.Processes.NotifyActionStatus($"无法结束进程：{ex.Message}");
        }
    }
}
