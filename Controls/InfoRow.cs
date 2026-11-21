using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUIApp.Controls;

/// <summary>
/// 键值信息行：左侧为次要文字标签，右侧为可选择的正文值。
/// </summary>
/// <remarks>
/// 标签列固定 132 px 在窄窗口上会占掉近三分之一的宽度，正文被挤成竖排文字。
/// 低于阈值时改为「标签在上、数值在下」，正文获得整行宽度。
/// </remarks>
public sealed class InfoRow : ContentControl
{
    /// <summary>低于此宽度时标签换行到数值上方（132 px 标签列已超过可用宽度的四成）。</summary>
    public const double StackThreshold = 300;

    private Grid? _layout;
    private TextBlock? _labelText;
    private TextBlock? _valueText;
    private bool? _stacked;

    public InfoRow()
    {
        SizeChanged += (_, _) => UpdateLayoutMode();
        Loaded += (_, _) => UpdateLayoutMode();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _layout = GetTemplateChild("InfoRowLayout") as Grid;
        _labelText = GetTemplateChild("InfoRowLabel") as TextBlock;
        _valueText = GetTemplateChild("InfoRowValue") as TextBlock;
        _stacked = null;
        UpdateLayoutMode();
    }

    private void UpdateLayoutMode()
    {
        if (_layout is null || _labelText is null || _valueText is null)
            return;

        double content = Breakpoints.ContentWidth(this);
        if (content <= 0)
            return;

        bool stacked = content < StackThreshold;
        if (_stacked == stacked)
            return;

        _stacked = stacked;

        if (stacked)
        {
            _layout.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            _layout.ColumnDefinitions[1].Width = new GridLength(0);

            Grid.SetRow(_valueText, 1);
            Grid.SetColumn(_valueText, 0);
            _valueText.Margin = new Thickness(0, 2, 0, 0);

            _labelText.Margin = new Thickness(0);
        }
        else
        {
            _layout.ColumnDefinitions[0].Width = new GridLength(132);
            _layout.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);

            Grid.SetRow(_valueText, 0);
            Grid.SetColumn(_valueText, 1);
            _valueText.Margin = new Thickness(0);

            _labelText.Margin = new Thickness(0, 1, 0, 0);
        }
    }

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
