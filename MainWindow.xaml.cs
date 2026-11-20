using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using WinUIApp.Services;
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
            _pages["settings"] = new SettingsPage();
        }

        public void SetViewModel(MainViewModel viewModel)
        {
            _viewModel = viewModel;
            MainNavigationView.DataContext = viewModel;

            // 为所有页面设置 DataContext
            foreach (var page in _pages.Values)
            {
                page.DataContext = viewModel;
            }

            var preferences = MonitorPreferences.Load();
            var settings = (SettingsPage)_pages["settings"];
            settings.Initialize(preferences);
            settings.PreferencesChanged += ApplyPreferences;
            ApplyPreferences(preferences);

            // 默认导航到总览页
            ContentFrame.Content = _pages["overview"];
            MainNavigationView.SelectedItem = NavOverview;
        }

        private void OnToggleMonitoringClicked(object sender, RoutedEventArgs e)
        {
            if (_viewModel is null) return;

            if (_viewModel.IsMonitoring)
                _viewModel.PauseMonitoring();
            else
                _viewModel.ResumeMonitoring();

            MonitoringToggleButton.Content = _viewModel.IsMonitoring ? "暂停" : "恢复";
        }

        private void ApplyPreferences(MonitorPreferences preferences)
        {
            MainNavigationView.RequestedTheme = preferences.Theme switch
            {
                "Dark" => ElementTheme.Dark,
                "Light" => ElementTheme.Light,
                _ => ElementTheme.Default
            };
            if (AppWindow.Presenter is OverlappedPresenter presenter)
                presenter.IsAlwaysOnTop = preferences.AlwaysOnTop;
            _viewModel?.SetRefreshInterval(preferences.IntervalSeconds);
        }

        private void OnOpenSettings(object sender, RoutedEventArgs args) =>
            MainNavigationView.SelectedItem = MainNavigationView.SettingsItem;

        private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Content = _pages["settings"];
                return;
            }
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
