using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using WinUIApp.ViewModels;
using WinUIApp.Views;

namespace WinUIApp
{
    public sealed partial class MainWindow : Window
    {
        private MainViewModel? _viewModel;
        private readonly Dictionary<string, Page> _pages = new();

        public MainWindow()
        {
            InitializeComponent();

            // 预创建所有页面
            _pages["overview"] = new OverviewPage();
            _pages["cpu"] = new CpuPage();
            _pages["memory"] = new MemoryPage();
            _pages["disk"] = new DiskPage();
            _pages["network"] = new NetworkPage();
            _pages["system"] = new SystemInfoPage();
        }

        public void SetViewModel(MainViewModel viewModel)
        {
            _viewModel = viewModel;

            // 为所有页面设置 DataContext
            foreach (var page in _pages.Values)
            {
                page.DataContext = viewModel;
            }

            // 默认导航到总览页
            ContentFrame.Content = _pages["overview"];
            MainNavigationView.SelectedItem = NavOverview;
        }

        private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                if (_pages.TryGetValue(tag, out var page))
                {
                    ContentFrame.Content = page;
                }
            }
        }
    }
}
