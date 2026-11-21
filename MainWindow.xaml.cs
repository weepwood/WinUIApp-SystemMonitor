using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;
using Windows.UI;
using WinUIApp.Services;
using WinUIApp.ViewModels;
using WinUIApp.Views;

namespace WinUIApp
{
    public sealed partial class MainWindow : Window
    {
        private readonly Dictionary<string, Page> _pages = new();
        private MainViewModel? _viewModel;
        private SettingsPage? _settingsPage;
        private MonitorPreferences _preferences = new();
        private ElementTheme? _appliedTheme;
        private string? _appliedBackdrop;

        public MainWindow()
        {
            InitializeComponent();
            Title = "系统监测";

            _preferences = MonitorPreferences.Load();

            // 预创建所有页面，切换导航时保留页面状态
            _pages["overview"] = new OverviewPage();
            _pages["cpu"] = new CpuPage();
            _pages["memory"] = new MemoryPage();
            _pages["disk"] = new DiskPage();
            _pages["network"] = new NetworkPage();
            _pages["system"] = new SystemInfoPage();
            _settingsPage = new SettingsPage();
            _pages["settings"] = _settingsPage;

            ConfigureTitleBar();
            AddSettingsAccelerator();
            ApplyPreferences(_preferences);
            ApplyWindowSize();

            AppWindow.Changed += OnAppWindowChanged;
            Activated += OnFirstActivated;
            Closed += OnWindowClosed;
        }

        public void SetViewModel(MainViewModel viewModel)
        {
            _viewModel = viewModel;
            MainNavigationView.DataContext = viewModel;

            // 为所有页面设置 DataContext
            foreach (var page in _pages.Values)
                page.DataContext = viewModel;

            if (_settingsPage != null)
            {
                _settingsPage.Initialize(_preferences);
                _settingsPage.PreferencesChanged += OnPreferencesChanged;
            }

            // 窗口最小化时可暂停采样
            viewModel.ShouldSkipSampling = () =>
                _preferences.PauseWhenMinimized
                && AppWindow.Presenter is OverlappedPresenter presenter
                && presenter.State == OverlappedPresenterState.Minimized;

            ApplyPreferences(_preferences);

            // 默认导航到总览页
            ContentFrame.Content = _pages["overview"];
            MainNavigationView.SelectedItem = NavOverview;
        }

        // OemComma 无法在 XAML 中解析为 VirtualKey，改用代码注册 Ctrl+,。
        private void AddSettingsAccelerator()
        {
            var accelerator = new KeyboardAccelerator
            {
                // WinRT 的 VirtualKey 没有 OemComma 成员，188 即 VK_OEM_COMMA。
                Key = (VirtualKey)188,
                Modifiers = VirtualKeyModifiers.Control,
            };
            accelerator.Invoked += OnOpenSettingsAccelerator;
            RootGrid.KeyboardAccelerators.Add(accelerator);
        }

        private void ConfigureTitleBar()
        {
            try
            {
                ExtendsContentIntoTitleBar = true;
                SetTitleBar(AppTitleBar);

                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    // 让标题栏按钮与 48 px 标题区等高，并隐藏系统图标/菜单。
                    try
                    {
                        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
                        AppWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
                    }
                    catch (Exception)
                    {
                        // Windows 10 可能不支持自定义标题栏高度，保持默认高度。
                    }

                    AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                }
            }
            catch (Exception)
            {
                // 自定义标题栏失败时退回系统标题栏，界面仍可正常使用。
            }
        }

        private void ApplyPreferences(MonitorPreferences preferences)
        {
            _preferences = preferences;

            ApplyTheme();
            ApplyBackdrop();

            if (AppWindow.Presenter is OverlappedPresenter presenter)
                presenter.IsAlwaysOnTop = preferences.AlwaysOnTop;

            _viewModel?.Configure(preferences);
        }

        private void ApplyTheme()
        {
            var theme = _preferences.Theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };

            if (_appliedTheme == theme)
                return;

            _appliedTheme = theme;
            RootGrid.RequestedTheme = theme;

            bool dark = theme == ElementTheme.Dark
                || (theme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Dark);
            ApplyTitleBarTheme(dark);
        }

        private void ApplyTitleBarTheme(bool dark)
        {
            try
            {
                if (!AppWindowTitleBar.IsCustomizationSupported())
                    return;

                var titleBar = AppWindow.TitleBar;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonForegroundColor = dark ? Colors.White : Color.FromArgb(255, 26, 26, 26);
                titleBar.ButtonHoverForegroundColor = dark ? Colors.White : Color.FromArgb(255, 0, 0, 0);
                titleBar.ButtonHoverBackgroundColor = dark
                    ? Color.FromArgb(24, 255, 255, 255)
                    : Color.FromArgb(16, 0, 0, 0);
                titleBar.ButtonPressedBackgroundColor = dark
                    ? Color.FromArgb(36, 255, 255, 255)
                    : Color.FromArgb(24, 0, 0, 0);
                titleBar.ButtonInactiveForegroundColor = dark
                    ? Color.FromArgb(120, 255, 255, 255)
                    : Color.FromArgb(120, 0, 0, 0);
            }
            catch (Exception)
            {
                // 标题栏着色仅为视觉细节，失败时保持系统默认颜色。
            }
        }

        private void ApplyBackdrop()
        {
            if (_appliedBackdrop == _preferences.Backdrop)
                return;

            _appliedBackdrop = _preferences.Backdrop;

            try
            {
                switch (_preferences.Backdrop)
                {
                    case "Solid":
                        SystemBackdrop = null;
                        SolidBackground.Visibility = Visibility.Visible;
                        break;
                    case "Acrylic":
                        SystemBackdrop = new DesktopAcrylicBackdrop();
                        SolidBackground.Visibility = Visibility.Collapsed;
                        break;
                    default:
                        // Mica 需要 Windows 11；Windows 10 上回退为亚克力。
                        bool micaSupported = Environment.OSVersion.Version.Build >= 22000;
                        SystemBackdrop = micaSupported ? new MicaBackdrop() : new DesktopAcrylicBackdrop();
                        SolidBackground.Visibility = Visibility.Collapsed;
                        break;
                }
            }
            catch (Exception)
            {
                // 材质不可用时退回纯色背景，保证文字对比度。
                SystemBackdrop = null;
                SolidBackground.Visibility = Visibility.Visible;
            }
        }

        private void ApplyWindowSize()
        {
            int width = _preferences.RememberWindowSize ? _preferences.WindowWidth : 1180;
            int height = _preferences.RememberWindowSize ? _preferences.WindowHeight : 800;

            try
            {
                var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
                if (workArea.Width > 0 && workArea.Height > 0)
                {
                    width = Math.Min(width, workArea.Width);
                    height = Math.Min(height, workArea.Height);
                }
            }
            catch (Exception)
            {
                // 取不到显示器信息时直接使用保存的尺寸。
            }

            AppWindow.Resize(new SizeInt32(Math.Max(760, width), Math.Max(520, height)));
        }

        private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
        {
            Activated -= OnFirstActivated;
            if (_preferences.StartMaximized && AppWindow.Presenter is OverlappedPresenter presenter)
                presenter.Maximize();
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (!args.DidSizeChange || !_preferences.RememberWindowSize)
                return;

            if (AppWindow.Presenter is OverlappedPresenter presenter
                && presenter.State != OverlappedPresenterState.Restored)
                return;

            var size = AppWindow.Size;
            if (size.Width < 700 || size.Height < 480)
                return;

            _preferences.WindowWidth = size.Width;
            _preferences.WindowHeight = size.Height;
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            AppWindow.Changed -= OnAppWindowChanged;
            _viewModel?.Dispose();
            _preferences.Save();
        }

        private void OnPreferencesChanged(MonitorPreferences preferences)
        {
            ApplyPreferences(preferences);
        }

        private void OnToggleMonitoringClicked(object sender, RoutedEventArgs e)
        {
            _viewModel?.ToggleMonitoring();
        }

        private void OnRefreshNowClicked(object sender, RoutedEventArgs e)
        {
            _viewModel?.RefreshNow();
        }

        private void OnOpenSettings(object sender, RoutedEventArgs args) => OpenSettingsPage();

        private void OpenSettingsPage()
        {
            MainNavigationView.SelectedItem = MainNavigationView.SettingsItem;
        }

        private void OnRefreshAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            _viewModel?.RefreshNow();
        }

        private void OnToggleMonitoringAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            _viewModel?.ToggleMonitoring();
        }

        private void OnOpenSettingsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            OpenSettingsPage();
        }

        private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Content = _pages["settings"];
                return;
            }

            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag
                && _pages.TryGetValue(tag, out var page))
            {
                ContentFrame.Content = page;
            }
        }
    }
}
