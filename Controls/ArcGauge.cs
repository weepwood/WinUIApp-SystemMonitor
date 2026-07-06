using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace WinUIApp.Controls;

/// <summary>
/// 弧形仪表盘控件，使用 WinUI Shapes 绘制
/// </summary>
public sealed class ArcGauge : UserControl
{
    private readonly Grid _rootGrid;
    private readonly Path _backgroundArc;
    private readonly Path _foregroundArc;
    private readonly TextBlock _titleText;
    private readonly TextBlock _subtitleText;

    public static readonly DependencyProperty PercentProperty =
        DependencyProperty.Register(nameof(Percent), typeof(double), typeof(ArcGauge),
            new PropertyMetadata(0.0, OnVisualPropertyChanged));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ArcGauge),
            new PropertyMetadata("Title", OnTitleChanged));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(ArcGauge),
            new PropertyMetadata("0%", OnSubtitleChanged));

    public static readonly DependencyProperty GaugeColorProperty =
        DependencyProperty.Register(nameof(GaugeColor), typeof(Color), typeof(ArcGauge),
            new PropertyMetadata(Colors.DodgerBlue, OnGaugeColorChanged));

    public static readonly DependencyProperty StrokeWidthProperty =
        DependencyProperty.Register(nameof(StrokeWidth), typeof(double), typeof(ArcGauge),
            new PropertyMetadata(18.0, OnVisualPropertyChanged));

    public double Percent
    {
        get => (double)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
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

    public Color GaugeColor
    {
        get => (Color)GetValue(GaugeColorProperty);
        set => SetValue(GaugeColorProperty, value);
    }

    public double StrokeWidth
    {
        get => (double)GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGauge gauge)
            gauge.UpdateGauge();
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGauge gauge)
            gauge._titleText.Text = (string)e.NewValue;
    }

    private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGauge gauge)
            gauge._subtitleText.Text = (string)e.NewValue;
    }

    private static void OnGaugeColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGauge gauge)
            gauge.UpdateGauge();
    }

    public ArcGauge()
    {
        _rootGrid = new Grid();
        _backgroundArc = new Path
        {
            Stroke = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        _foregroundArc = new Path
        {
            Stroke = new SolidColorBrush(Colors.DodgerBlue),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        _titleText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 14,
            Foreground = new SolidColorBrush(Colors.White),
            Margin = new Thickness(0, -8, 0, 0),
        };
        _subtitleText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            Foreground = new SolidColorBrush(Colors.Gray),
            Margin = new Thickness(0, 16, 0, 0),
        };

        _rootGrid.Children.Add(_backgroundArc);
        _rootGrid.Children.Add(_foregroundArc);
        _rootGrid.Children.Add(_titleText);
        _rootGrid.Children.Add(_subtitleText);

        Content = _rootGrid;
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGauge();
    }

    private void UpdateGauge()
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        double minDim = Math.Min(width, height);
        double strokeWidth = Math.Max(StrokeWidth, minDim * 0.08);
        double radius = (minDim - strokeWidth) / 2.0;
        if (radius <= 0) return;

        double centerX = width / 2.0;
        double centerY = height / 2.0;

        // 角度定义: 从 135° 到 405° (即 135° + 270°)
        double startAngleDeg = 135.0;
        double sweepAngleDeg = 270.0;

        double startAngleRad = startAngleDeg * Math.PI / 180.0;
        double sweepAngleRad = sweepAngleDeg * Math.PI / 180.0;
        double endAngleRad = startAngleRad + sweepAngleRad;

        // 背景弧
        _backgroundArc.Data = CreateArcGeometry(centerX, centerY, radius, startAngleRad, sweepAngleRad);
        _backgroundArc.StrokeThickness = strokeWidth;
        ((SolidColorBrush)_backgroundArc.Stroke).Color = Color.FromArgb(40, 128, 128, 128);

        // 前景弧（百分比）
        float pct = (float)Math.Clamp(Percent / 100.0, 0.0, 1.0);
        double fillSweep = sweepAngleRad * pct;

        _foregroundArc.Data = fillSweep > 0.001
            ? CreateArcGeometry(centerX, centerY, radius, startAngleRad, fillSweep)
            : null;
        _foregroundArc.StrokeThickness = strokeWidth;
        ((SolidColorBrush)_foregroundArc.Stroke).Color = GaugeColor;

        // 调整文字大小
        _titleText.FontSize = minDim * 0.14;
        _subtitleText.FontSize = minDim * 0.08;
        _titleText.Margin = new Thickness(0, -minDim * 0.06, 0, 0);
        _subtitleText.Margin = new Thickness(0, minDim * 0.08, 0, 0);
    }

    private static PathGeometry CreateArcGeometry(double cx, double cy, double radius, double startAngleRad, double sweepAngleRad)
    {
        double endAngleRad = startAngleRad + sweepAngleRad;

        double x1 = cx + radius * Math.Cos(startAngleRad);
        double y1 = cy + radius * Math.Sin(startAngleRad);
        double x2 = cx + radius * Math.Cos(endAngleRad);
        double y2 = cy + radius * Math.Sin(endAngleRad);

        bool isLargeArc = sweepAngleRad > Math.PI;

        var pathFigure = new PathFigure { StartPoint = new Windows.Foundation.Point(x1, y1) };
        var arcSegment = new ArcSegment
        {
            Point = new Windows.Foundation.Point(x2, y2),
            Size = new Windows.Foundation.Size(radius, radius),
            IsLargeArc = isLargeArc,
            SweepDirection = SweepDirection.Clockwise,
        };
        pathFigure.Segments.Add(arcSegment);

        var pathGeometry = new PathGeometry();
        pathGeometry.Figures.Add(pathFigure);
        return pathGeometry;
    }
}
