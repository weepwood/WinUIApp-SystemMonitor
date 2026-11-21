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
            InitializeComponent();

            string? diagnosticPath = Environment.GetEnvironmentVariable("WINUI_STARTUP_LOG");
            if (!string.IsNullOrWhiteSpace(diagnosticPath))
            {
                AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
                    WriteStartupDiagnostic(diagnosticPath, eventArgs.ExceptionObject?.ToString() ?? "Unhandled exception");
                UnhandledException += (_, eventArgs) =>
                    WriteStartupDiagnostic(diagnosticPath, eventArgs.Exception?.ToString() ?? eventArgs.Message);
            }
        }

        private static void WriteStartupDiagnostic(string path, string message)
        {
            try
            {
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
            catch (Exception)
            {
                // Diagnostic logging must never interfere with shutdown.
            }
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
