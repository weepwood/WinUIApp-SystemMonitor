using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUIApp.Controls;

/// <summary>
/// 页面顶部标题区：眉题、标题、副标题与右侧操作区。
/// </summary>
/// <remarks>
/// 窗口变窄时，右侧操作区改为在标题下方另起一行：
/// 原来标题与按钮挤在同一行，副标题被压缩到两三行、按钮被裁掉的情况经常出现。
/// 判定使用控件自身宽度，因此导航栏展开 / 折叠后的可用空间也能正确响应。
/// </remarks>
public sealed class PageHeader : ContentControl
{
    /// <summary>低于此宽度时操作区换行到标题下方。</summary>
    public const double StackThreshold = Breakpoints.Collapse;

    public static readonly DependencyProperty EyebrowProperty = DependencyProperty.Register(
        nameof(Eyebrow), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    private Grid? _layout;
    private ContentPresenter? _actions;
    private bool? _stacked;

    public PageHeader()
    {
        SizeChanged += (_, _) => UpdateLayoutMode();
        Loaded += (_, _) => UpdateLayoutMode();
    }

    public string Eyebrow
    {
        get => (string)GetValue(EyebrowProperty);
        set => SetValue(EyebrowProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _layout = GetTemplateChild("HeaderGrid") as Grid;
        _actions = GetTemplateChild("HeaderActions") as ContentPresenter;
        _stacked = null;
        UpdateLayoutMode();
    }

    private void UpdateLayoutMode()
    {
        if (_actions is null)
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
            Grid.SetRow(_actions, 1);
            Grid.SetColumn(_actions, 0);
            Grid.SetColumnSpan(_actions, 2);
            _actions.HorizontalAlignment = HorizontalAlignment.Left;
            _actions.Margin = new Thickness(0, 10, 0, 0);
        }
        else
        {
            Grid.SetRow(_actions, 0);
            Grid.SetColumn(_actions, 1);
            Grid.SetColumnSpan(_actions, 1);
            _actions.HorizontalAlignment = HorizontalAlignment.Right;
            _actions.Margin = new Thickness(16, 0, 0, 0);
        }

        if (_layout?.RowDefinitions.Count > 1)
        {
            // 堆叠时第二行需要实际高度；并排时收掉空行，避免多出一段空白。
            _layout.RowDefinitions[1].Height = stacked
                ? GridLength.Auto
                : new GridLength(0);
        }
    }
}
