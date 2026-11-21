using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUIApp.Controls;

/// <summary>
/// Fluent「设置卡」容器：左侧图标、中部标题与说明、右侧操作控件。
/// 外观由 App.xaml 中的隐式样式提供，自动跟随浅色/深色主题。
/// </summary>
public sealed class SettingsCard : ContentControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(SettingsCard), new PropertyMetadata("\uE713"));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Segoe Fluent Icons 字形，例如 "\uE790"。</summary>
    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}
