using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIApp.Controls;
using WinUIApp.ViewModels;

namespace WinUIApp.Views;

public sealed partial class MemoryPage : Page
{
    public MemoryPage()
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
