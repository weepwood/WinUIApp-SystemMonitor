using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUIApp.Controls;

/// <summary>
/// Fluent「设置卡」容器：左侧图标、中部标题与说明、右侧操作控件。
/// 外观由 App.xaml 中的隐式样式提供，自动跟随浅色/深色主题。
/// </summary>
/// <remarks>
/// 窄窗口下标题与控件并排会让说明文字被压成一列单字，
/// 因此控件改用整行宽度放在标题下方（设置页几十张卡片都会受益）。
/// </remarks>
public sealed class SettingsCard : ContentControl
{
    /// <summary>低于此宽度时右侧控件换行到说明文字下方。</summary>
    public const double StackThreshold = Breakpoints.Compact;

    private Grid? _layout;
    private ContentPresenter? _contentPresenter;
    private bool? _stacked;

    public SettingsCard()
    {
        SizeChanged += (_, _) => UpdateLayoutMode();
        Loaded += (_, _) => UpdateLayoutMode();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _layout = GetTemplateChild("SettingsCardLayout") as Grid;
        _contentPresenter = GetTemplateChild("SettingsCardContent") as ContentPresenter;
        _stacked = null;
        UpdateLayoutMode();
    }

    private void UpdateLayoutMode()
    {
        if (_contentPresenter is null)
            return;

        double width = Breakpoints.ContentWidth(this);
        if (width <= 0)
            return;

        bool stacked = width < StackThreshold;
        if (_stacked == stacked)
            return;

        _stacked = stacked;

        if (stacked)
        {
            Grid.SetRow(_contentPresenter, 1);
            Grid.SetColumn(_contentPresenter, 1);
            Grid.SetColumnSpan(_contentPresenter, 2);
            _contentPresenter.HorizontalAlignment = HorizontalAlignment.Left;
            _contentPresenter.Margin = new Thickness(0, 10, 0, 0);
        }
        else
        {
            Grid.SetRow(_contentPresenter, 0);
            Grid.SetColumn(_contentPresenter, 2);
            Grid.SetColumnSpan(_contentPresenter, 1);
            _contentPresenter.HorizontalAlignment = HorizontalAlignment.Right;
            _contentPresenter.Margin = new Thickness(0);
        }

        if (_layout?.RowDefinitions.Count > 1)
            _layout.RowDefinitions[1].Height = stacked ? GridLength.Auto : new GridLength(0);
    }

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
