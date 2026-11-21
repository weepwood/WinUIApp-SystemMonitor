using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace WinUIApp.Services;

/// <summary>
/// 通过 HKCU 的 Run 键注册开机启动。仅对未打包的 exe 有意义，
/// 失败时返回错误说明，由设置页显示提示。
/// </summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SystemMonitor";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            return false;
        }
    }

    public static bool TrySetEnabled(bool enabled, out string? error)
    {
        error = null;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                error = "无法打开开机启动注册表项。";
                return false;
            }

            if (enabled)
            {
                string? executable = Environment.ProcessPath;
                if (string.IsNullOrEmpty(executable))
                {
                    error = "无法确定应用程序的可执行文件路径。";
                    return false;
                }

                key.SetValue(ValueName, $"\"{executable}\"", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            error = ex.Message;
            return false;
        }
    }
}
