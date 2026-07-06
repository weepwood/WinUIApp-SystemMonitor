using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using WinUIApp.ViewModels;

namespace WinUIApp
{
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var mainWindow = new MainWindow();

            // 创建 ViewModel 并启动监测
            var viewModel = new MainViewModel(mainWindow.DispatcherQueue);
            mainWindow.SetViewModel(viewModel);
            viewModel.StartMonitoring();

            // 窗口关闭时停止监测
            mainWindow.Closed += (s, e) =>
            {
                viewModel.Dispose();
            };

            _window = mainWindow;
            _window.Activate();
        }
    }
}
