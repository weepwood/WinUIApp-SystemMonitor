using System;
using System.Collections.Generic;
using System.Linq;

namespace WinUIApp.ViewModels;

/// <summary>A bounded window of equally spaced sample indices, not wall-clock seconds.</summary>
public sealed class UsageHistory : BaseViewModel
{
    public const int Capacity = 60;
    private readonly Queue<float> _window = new(Capacity);
    public float[] Samples { get; private set; } = Array.Empty<float>();
    public string Summary { get; private set; } = "等待首次采样";

    public void Append(float value)
    {
        if (_window.Count == Capacity) _window.Dequeue();
        _window.Enqueue(value);
        Samples = _window.ToArray();
        Summary = $"{Samples.Length} 个采样点  ·  峰值 {Samples.Max():0.0}%";
        OnPropertyChanged(nameof(Samples));
        OnPropertyChanged(nameof(Summary));
    }
}
