using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIApp.Controls;
using WinUIApp.ViewModels;

namespace WinUIApp.Views;

public sealed partial class CpuPage : Page
{
    public CpuPage()
    {
        InitializeComponent();
        PageSizing.Attach(ContentScroller, ContentPanel);
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel monitor)
            monitor.RefreshNow();
    }
}
