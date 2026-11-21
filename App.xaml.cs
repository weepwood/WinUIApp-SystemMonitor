using Microsoft.UI.Xaml;
using WinUIApp.ViewModels;

namespace WinUIApp
{
    public partial class App : Application
    {
        private Window? _window;

        /// <summary>
        /// 界面调度队列。ViewModel 在后台线程采到变化后用它回到 UI 线程，
        /// 避免把没有变化的采样值整批派发到 UI 线程。
        /// </summary>
        public static Microsoft.UI.Dispatching.DispatcherQueue? UiDispatcher { get; private set; }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var mainWindow = new MainWindow();
            UiDispatcher = mainWindow.DispatcherQueue;

            // 创建 ViewModel 并启动监测；窗口关闭时由 MainWindow 负责停止与保存。
            var viewModel = new MainViewModel(mainWindow.DispatcherQueue);
            mainWindow.SetViewModel(viewModel);
            viewModel.StartMonitoring();

            _window = mainWindow;
            _window.Activate();
        }
    }
}
