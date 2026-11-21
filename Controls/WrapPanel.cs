using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace WinUIApp.Controls;

/// <summary>
/// 左到右排列、放不下就换行的面板。
/// <para>
/// 工具栏与操作按钮组原来写死在一行 Grid 里：窗口一窄就横向溢出，
/// 按钮被裁掉也无法点到。换成换行面板后，宽度不足时按钮自动落到下一行。
/// </para>
/// </summary>
public sealed class WrapPanel : Panel
{
    public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
        nameof(HorizontalSpacing), typeof(double), typeof(WrapPanel),
        new PropertyMetadata(8.0, OnLayoutChanged));

    public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(
        nameof(VerticalSpacing), typeof(double), typeof(WrapPanel),
        new PropertyMetadata(8.0, OnLayoutChanged));

    private readonly List<Rect> _bounds = new();
    private double _measuredWidth = double.NaN;

    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WrapPanel panel)
        {
            panel._bounds.Clear();
            panel._measuredWidth = double.NaN;
            panel.InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsFinite(availableSize.Width) && availableSize.Width > 0
            ? availableSize.Width
            : double.PositiveInfinity;

        double height = Reflow(width);

        return new Size(
            double.IsFinite(width) ? width : NaturalWidth(),
            height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // 测量与排布之间宽度变化（滚动条出现、父容器先 Auto 后固定）时，
        // 按最终宽度重排一次，避免按钮超出右边界。
        if (!double.IsFinite(_measuredWidth) || Math.Abs(finalSize.Width - _measuredWidth) > 0.5)
            Reflow(finalSize.Width);

        int index = 0;
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
                continue;

            if (index >= _bounds.Count)
                break;

            var bounds = _bounds[index++];
            child.Arrange(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        }

        return finalSize;
    }

    private double Reflow(double maxWidth)
    {
        _measuredWidth = maxWidth;
        _bounds.Clear();

        double x = 0;
        double y = 0;
        double rowHeight = 0;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
                continue;

            child.Measure(new Size(maxWidth, double.PositiveInfinity));
            var desired = child.DesiredSize;

            if (x > 0 && x + desired.Width > maxWidth)
            {
                y += rowHeight + VerticalSpacing;
                x = 0;
                rowHeight = 0;
            }

            _bounds.Add(new Rect(x, y, desired.Width, desired.Height));
            x += desired.Width + HorizontalSpacing;
            rowHeight = Math.Max(rowHeight, desired.Height);
        }

        return y + rowHeight;
    }

    private double NaturalWidth()
    {
        double widest = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Collapsed)
                widest = Math.Max(widest, child.DesiredSize.Width);
        }

        return widest;
    }
}
