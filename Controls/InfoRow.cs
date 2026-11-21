using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUIApp.Controls;

/// <summary>
/// 键值信息行：左侧为次要文字标签，右侧为可选择的正文值。
/// </summary>
public sealed class InfoRow : ContentControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(InfoRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(InfoRow), new PropertyMetadata(string.Empty));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
}
