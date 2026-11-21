using Microsoft.UI.Xaml;
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

            // 创建 ViewModel 并启动监测；窗口关闭时由 MainWindow 负责停止与保存。
            var viewModel = new MainViewModel(mainWindow.DispatcherQueue);
            mainWindow.SetViewModel(viewModel);
            viewModel.StartMonitoring();

            _window = mainWindow;
            _window.Activate();
        }
    }
}
