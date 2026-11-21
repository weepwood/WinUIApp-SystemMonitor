using System;
using System.IO;
using Microsoft.UI.Xaml;
using WinUIApp.ViewModels;

namespace WinUIApp
{
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            string? path = Environment.GetEnvironmentVariable("WINUI_STARTUP_LOG");
            if (!string.IsNullOrWhiteSpace(path))
            {
                AppDomain.CurrentDomain.UnhandledException += (_, e) => TraceStartup(path, e.ExceptionObject?.ToString() ?? "Unhandled exception");
                UnhandledException += (_, e) => TraceStartup(path, e.Exception?.ToString() ?? e.Message);
                TraceStartup(path, "App constructor: before XAML initialization");
            }

            InitializeComponent();
            if (!string.IsNullOrWhiteSpace(path))
                TraceStartup(path, "App constructor: XAML initialized");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            TraceStartup("OnLaunched: before MainWindow");
            var mainWindow = new MainWindow();
            TraceStartup("OnLaunched: MainWindow created");

            // 创建 ViewModel 并启动监测；窗口关闭时由 MainWindow 负责停止与保存。
            var viewModel = new MainViewModel(mainWindow.DispatcherQueue);
            TraceStartup("OnLaunched: ViewModel created");
            mainWindow.SetViewModel(viewModel);
            TraceStartup("OnLaunched: ViewModel assigned");
            viewModel.StartMonitoring();
            TraceStartup("OnLaunched: monitoring started");

            _window = mainWindow;
            _window.Activate();
            TraceStartup("OnLaunched: window activated");
        }

        private static void TraceStartup(string message)
        {
            string? path = Environment.GetEnvironmentVariable("WINUI_STARTUP_LOG");
            if (!string.IsNullOrWhiteSpace(path))
                TraceStartup(path, message);
        }

        private static void TraceStartup(string path, string message)
        {
            try
            {
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
            catch (Exception)
            {
                // Startup diagnostics are opt-in and must never affect normal launch.
            }
        }
    }
}
