using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIApp.ViewModels;

namespace WinUIApp.Views;

public sealed partial class CpuPage : Page
{
    public CpuPage()
    {
        InitializeComponent();
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel monitor)
            monitor.RefreshNow();
    }
}
