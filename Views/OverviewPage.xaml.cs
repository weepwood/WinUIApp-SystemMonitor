using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinUIApp.ViewModels;

namespace WinUIApp.Views;

public sealed partial class OverviewPage : Page
{
    public OverviewPage()
    {
        InitializeComponent();
    }

    private void OnCopySummary(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel monitor || !monitor.HasSample) return;
        try
        {
            var package = new DataPackage();
            package.SetText(monitor.CreateSummary());
            Clipboard.SetContent(package);
            CopyFeedback.Title = $"监测摘要已复制（{monitor.LastUpdatedText}）";
            CopyFeedback.Severity = InfoBarSeverity.Success;
        }
        catch (Exception)
        {
            CopyFeedback.Title = "无法写入剪贴板，请重试。";
            CopyFeedback.Severity = InfoBarSeverity.Warning;
        }
        CopyFeedback.IsOpen = true;
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel monitor)
            monitor.RefreshNow();
    }
}
