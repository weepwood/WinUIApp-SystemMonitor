using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinUIApp.ViewModels;

/// <summary>
/// ViewModel 基类，实现 INotifyPropertyChanged。
/// </summary>
/// <remarks>
/// <para>
/// 采样每秒触发一次，绝大多数属性值在相邻两拍之间并不会变化。
/// <see cref="SetProperty{T}"/> 对文本字段同样生效：格式化结果与上一拍相同时
/// 不会发通知，绑定系统因此可以跳过无变化的 UI 更新。
/// </para>
/// <para>
/// 采集在后台线程完成，只有发生变化的值才会被派发到 UI 线程写入。
/// 之前每一拍都会把「所有指标 + 所有文本」整体切回 UI 线程重写一遍，
/// 即使屏幕上没有任何像素需要改变。
/// </para>
/// </remarks>
public class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly int _uiThreadId = Environment.CurrentManagedThreadId;

    /// <summary>当前是否在创建 ViewModel 的线程（UI 线程）上。</summary>
    protected bool IsOnUiThread => Environment.CurrentManagedThreadId == _uiThreadId;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 写入字段并在值变化时发出通知。在后台线程调用时会先切回 UI 线程，
    /// 保证绑定更新与 XAML 属性写入发生在正确的线程上。
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;

        if (IsOnUiThread)
            OnPropertyChanged(propertyName);
        else
            Dispatch(() => OnPropertyChanged(propertyName));

        return true;
    }

    /// <summary>
    /// 一次发出多个属性通知；<see cref="SetProperty{T}"/> 已经做过相等判断，
    /// 调用方只需在数值确实变化时列出依赖它的次要属性。
    /// </summary>
    protected void OnPropertiesChanged(params string[] propertyNames)
    {
        if (PropertyChanged is null)
            return;

        if (IsOnUiThread)
        {
            foreach (string name in propertyNames)
                OnPropertyChanged(name);
            return;
        }

        Dispatch(() =>
        {
            foreach (string name in propertyNames)
                OnPropertyChanged(name);
        });
    }

    /// <summary>
    /// 在 UI 线程执行集合操作（<c>ObservableCollection</c> 只能在 UI 线程修改）。
    /// 已在 UI 线程时同步执行，否则排队并在下一拍执行。
    /// </summary>
    protected void RunOnUiThread(Action action)
    {
        if (IsOnUiThread)
        {
            action();
            return;
        }

        Dispatch(action);
    }

    private void Dispatch(Action action)
    {
        var queue = WinUIApp.App.UiDispatcher;
        if (queue is null || !queue.TryEnqueue(() => action()))
        {
            // 没有 UI 线程可用（例如单元测试）时直接执行。
            action();
        }
    }
}
