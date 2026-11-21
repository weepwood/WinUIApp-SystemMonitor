using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace WinUIApp.Controls;

/// <summary>
/// 按内容宽度自动折行的卡片 / 磁贴面板。
/// <para>
/// 用单个布局容器替代「多个 Grid + 若干 <c>VisualStateManager</c> 定位 Setter」：
/// 卡片位置由面板在布局阶段一次算出，窗口缩放时只重排一次，
/// 不必为每个断点维护一份 Grid.Row / Grid.Column 组合
/// （总览页原来 8 张卡片就有 24 个 Setter）。
/// </para>
/// <para>
/// 标了 <see cref="SpanFullWidthProperty"/> 的子元素独占整行，用于必须通栏的卡片。
/// </para>
/// </summary>
public sealed class ResponsivePanel : Panel
{
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(ResponsivePanel),
        new PropertyMetadata(240.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(
        nameof(MaxColumns), typeof(int), typeof(ResponsivePanel),
        new PropertyMetadata(4, OnLayoutPropertyChanged));

    public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
        nameof(HorizontalSpacing), typeof(double), typeof(ResponsivePanel),
        new PropertyMetadata(12.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(
        nameof(VerticalSpacing), typeof(double), typeof(ResponsivePanel),
        new PropertyMetadata(12.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty PreferEvenColumnsProperty = DependencyProperty.Register(
        nameof(PreferEvenColumns), typeof(bool), typeof(ResponsivePanel),
        new PropertyMetadata(false, OnLayoutPropertyChanged));

    public static readonly DependencyProperty StretchItemsProperty = DependencyProperty.Register(
        nameof(StretchItems), typeof(bool), typeof(ResponsivePanel),
        new PropertyMetadata(true, OnLayoutPropertyChanged));

    public static readonly DependencyProperty SpanFullWidthProperty = DependencyProperty.RegisterAttached(
        "SpanFullWidth", typeof(bool), typeof(ResponsivePanel),
        new PropertyMetadata(false, OnLayoutPropertyChanged));

    private readonly List<LayoutSlot> _slots = new();
    private List<double> _rowOffsets = new();
    private double _layoutWidth = double.NaN;

    /// <summary>单张卡片的最小宽度。低于此值时减少列数，而不是把卡片压扁。</summary>
    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    /// <summary>最大列数：宽屏下不再继续增加列，避免卡片过窄、留白过多。</summary>
    public int MaxColumns
    {
        get => (int)GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

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

    /// <summary>
    /// 是否避免「末行只剩一张」的排布。例如 4 张指标卡在只能放 3 列时会变成 3 + 1，
    /// 打开此选项后改为 2 列 × 2 行；能整除时（4 列放 4 张）不受影响。
    /// </summary>
    public bool PreferEvenColumns
    {
        get => (bool)GetValue(PreferEvenColumnsProperty);
        set => SetValue(PreferEvenColumnsProperty, value);
    }

    /// <summary>同一行的卡片拉齐到相同高度，避免并排时高低不齐。</summary>
    public bool StretchItems
    {
        get => (bool)GetValue(StretchItemsProperty);
        set => SetValue(StretchItemsProperty, value);
    }

    public static bool GetSpanFullWidth(UIElement element) =>
        (bool)element.GetValue(SpanFullWidthProperty);

    public static void SetSpanFullWidth(UIElement element, bool value) =>
        element.SetValue(SpanFullWidthProperty, value);

    /// <summary>最近一次布局使用的列数。</summary>
    public int ColumnCount { get; private set; } = 1;

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResponsivePanel panel)
        {
            panel._slots.Clear();
            panel._layoutWidth = double.NaN;
            panel.InvalidateMeasure();
        }
        else if (d is FrameworkElement element
            && Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element) is ResponsivePanel parent)
        {
            parent._layoutWidth = double.NaN;
            parent.InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        bool widthKnown = double.IsFinite(availableSize.Width) && availableSize.Width > 0;
        double width = widthKnown ? availableSize.Width : ResolveMinItemWidth();

        Layout(width);
        double height = _rowOffsets.Count == 0 ? 0 : _rowOffsets[^1] + _lastRowHeight;
        return new Size(widthKnown ? availableSize.Width : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // 测量与排布之间宽度可能变化（例如滚动条出现），按最终宽度重排一次，
        // 保证卡片既不会重叠也不会留下空列。
        if (double.IsNaN(_layoutWidth) || Math.Abs(finalSize.Width - _layoutWidth) > 0.5)
            Layout(finalSize.Width);

        foreach (var slot in _slots)
        {
            double y = slot.Row < _rowOffsets.Count ? _rowOffsets[slot.Row] : 0;
            double height = slot.Element.DesiredSize.Height;
            if (StretchItems && slot.Row < _rowHeights.Count && _rowHeights[slot.Row] > height)
                height = _rowHeights[slot.Row];

            slot.Element.Arrange(new Rect(slot.X, y, slot.Width, height));
        }

        return finalSize;
    }

    private double _lastRowHeight;
    private List<double> _rowHeights = new();

    private void Layout(double width)
    {
        _layoutWidth = width;
        _slots.Clear();
        _rowOffsets.Clear();

        int itemCount = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Collapsed && !GetSpanFullWidth(child))
                itemCount++;
        }

        int columns = ResolveColumns(width, itemCount);
        ColumnCount = columns;
        double itemWidth = ResolveItemWidth(width, columns);

        var rowHeights = new List<double>();
        int row = 0;
        int column = 0;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
                continue;

            bool span = GetSpanFullWidth(child);

            // 通栏元素必须从新行的行首开始。
            if (span && column != 0)
            {
                row++;
                column = 0;
            }

            double slotWidth = span ? width : itemWidth;
            child.Measure(new Size(slotWidth, double.PositiveInfinity));

            double x = span ? 0 : column * (itemWidth + HorizontalSpacing);
            _slots.Add(new LayoutSlot(child, row, x, slotWidth));

            while (rowHeights.Count <= row)
                rowHeights.Add(0);
            rowHeights[row] = Math.Max(rowHeights[row], child.DesiredSize.Height);

            if (span)
            {
                row++;
                column = 0;
            }
            else if (++column >= columns)
            {
                row++;
                column = 0;
            }
        }

        double y = 0;
        for (int i = 0; i < rowHeights.Count; i++)
        {
            _rowOffsets.Add(y);
            y += rowHeights[i];
            if (i < rowHeights.Count - 1)
                y += VerticalSpacing;
        }

        _lastRowHeight = rowHeights.Count > 0 ? rowHeights[^1] : 0;
        _rowHeights = rowHeights;
    }

    private int ResolveColumns(double width, int itemCount)
    {
        int maxColumns = Math.Max(1, MaxColumns);
        double minItem = ResolveMinItemWidth();
        int columns = Math.Clamp(Breakpoints.ColumnCount(width, minItem), 1, maxColumns);

        // 4 张卡片在 3 列下会剩一张独占一行；减半后成为 2 × 2。
        if (PreferEvenColumns && columns >= 3 && itemCount > columns && itemCount % columns != 0)
        {
            // 只在减半后仍然放得下时缩小列数，避免把卡片压到最小宽度以下。
            int half = columns / 2;
            double halfItem = (width - HorizontalSpacing * (half - 1)) / half;
            if (half >= 2 && itemCount > half && itemCount % half == 0 && halfItem >= minItem)
                columns = half;
        }

        return columns;
    }

    private double ResolveItemWidth(double width, int columns)
    {
        if (columns <= 1)
            return Math.Max(0, width);

        double item = (width - HorizontalSpacing * (columns - 1)) / columns;
        return item > 0 ? item : 0;
    }

    private double ResolveMinItemWidth() => MinItemWidth > 0 ? MinItemWidth : 240;

    private readonly record struct LayoutSlot(UIElement Element, int Row, double X, double Width);
}
