using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        _isLoading = false;
    }

    private void OnSortSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || DataContext is not MainViewModel monitor)
            return;

        monitor.Processes.SortMode = Math.Max(0, SortSelector.SelectedIndex);
        ProcessViewChanged?.Invoke();
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

        var package = new DataPackage();
        package.SetText(monitor.Processes.CreateListText());
        Clipboard.SetContent(package);
        monitor.Processes.SetCopyStatus("已复制");
    }
}
