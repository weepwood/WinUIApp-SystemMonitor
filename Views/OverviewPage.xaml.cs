using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinUIApp.Controls;
using WinUIApp.ViewModels;

namespace WinUIApp.Views;

public sealed partial class OverviewPage : Page
{
    public OverviewPage()
    {
        InitializeComponent();

        // 页面留白随窗口宽度收缩，窄窗口下把空间让给内容。
        PageSizing.Attach(ContentScroller, ContentPanel);
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
