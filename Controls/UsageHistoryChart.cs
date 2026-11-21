using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace WinUIApp.Controls;

/// <summary>
/// 使用率趋势图。折线经过 Catmull-Rom 平滑处理，面积填充使用同色渐变，
/// 网格与文字颜色随系统主题自动切换，并支持网格线、面积填充与阈值参考线开关。
/// </summary>
public sealed class UsageHistoryChart : UserControl
{
    private const double MarginLeft = 38;
    private const double MarginRight = 8;
    private const double MarginTop = 10;
    private const double MarginBottom = 8;
    private const int GridLineCount = 5;

    private readonly Canvas _canvas = new() { IsHitTestVisible = false };
    private readonly Line[] _gridLines = new Line[GridLineCount];
    private readonly TextBlock[] _yLabels = new TextBlock[GridLineCount];
    private readonly Path _areaPath = new();
    private readonly Path _linePath = new();
    private readonly Line _thresholdLine = new();
    private readonly TextBlock _thresholdLabel = new();
    private readonly Ellipse _endDot = new();
    private readonly TextBlock _emptyHint = new();
    private readonly SolidColorBrush _gridBrush = new();
    private readonly SolidColorBrush _labelBrush = new();
    private readonly SolidColorBrush _thresholdBrush = new();
    private readonly SolidColorBrush _lineBrush = new();

    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(float[]), typeof(UsageHistoryChart),
        new PropertyMetadata(Array.Empty<float>(), OnVisualChanged));

    public static readonly DependencyProperty AccentColorProperty = DependencyProperty.Register(
        nameof(AccentColor), typeof(Color), typeof(UsageHistoryChart),
        new PropertyMetadata(Colors.DodgerBlue, OnVisualChanged));

    public static readonly DependencyProperty CapacityProperty = DependencyProperty.Register(
        nameof(Capacity), typeof(int), typeof(UsageHistoryChart),
        new PropertyMetadata(60, OnVisualChanged));

    public static readonly DependencyProperty ShowGridLinesProperty = DependencyProperty.Register(
        nameof(ShowGridLines), typeof(bool), typeof(UsageHistoryChart),
        new PropertyMetadata(true, OnVisualChanged));

    public static readonly DependencyProperty ShowAreaFillProperty = DependencyProperty.Register(
        nameof(ShowAreaFill), typeof(bool), typeof(UsageHistoryChart),
        new PropertyMetadata(true, OnVisualChanged));

    public static readonly DependencyProperty ShowThresholdLineProperty = DependencyProperty.Register(
        nameof(ShowThresholdLine), typeof(bool), typeof(UsageHistoryChart),
        new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty ThresholdProperty = DependencyProperty.Register(
        nameof(Threshold), typeof(double), typeof(UsageHistoryChart),
        new PropertyMetadata(80.0, OnVisualChanged));

    public float[] Data
    {
        get => (float[])GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    /// <summary>横轴容量（采样点上限），与 UsageHistory 的容量保持一致。</summary>
    public int Capacity
    {
        get => (int)GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    public bool ShowGridLines
    {
        get => (bool)GetValue(ShowGridLinesProperty);
        set => SetValue(ShowGridLinesProperty, value);
    }

    public bool ShowAreaFill
    {
        get => (bool)GetValue(ShowAreaFillProperty);
        set => SetValue(ShowAreaFillProperty, value);
    }

    public bool ShowThresholdLine
    {
        get => (bool)GetValue(ShowThresholdLineProperty);
        set => SetValue(ShowThresholdLineProperty, value);
    }

    /// <summary>阈值参考线的百分比位置（0-100）。</summary>
    public double Threshold
    {
        get => (double)GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UsageHistoryChart chart)
            chart.UpdateChart();
    }

    public UsageHistoryChart()
    {
        for (int i = 0; i < GridLineCount; i++)
        {
            var gridLine = new Line
            {
                Stroke = _gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 3 },
            };
            _gridLines[i] = gridLine;
            _canvas.Children.Add(gridLine);

            var label = new TextBlock { FontSize = 10, Foreground = _labelBrush, Text = $"{100 - i * 25}%" };
            _yLabels[i] = label;
            _canvas.Children.Add(label);
        }

        _thresholdLine.Stroke = _thresholdBrush;
        _thresholdLine.StrokeThickness = 1;
        _thresholdLine.StrokeDashArray = new DoubleCollection { 5, 4 };
        _canvas.Children.Add(_thresholdLine);

        _areaPath.StrokeThickness = 0;
        _canvas.Children.Add(_areaPath);

        _linePath.Stroke = _lineBrush;
        _linePath.StrokeThickness = 2;
        _linePath.StrokeLineJoin = PenLineJoin.Round;
        _linePath.StrokeStartLineCap = PenLineCap.Round;
        _linePath.StrokeEndLineCap = PenLineCap.Round;
        _canvas.Children.Add(_linePath);

        _endDot.Width = 6;
        _endDot.Height = 6;
        _endDot.Fill = _lineBrush;
        _canvas.Children.Add(_endDot);

        _thresholdLabel.FontSize = 9;
        _thresholdLabel.Foreground = _thresholdBrush;
        _canvas.Children.Add(_thresholdLabel);

        _emptyHint.Text = "等待首次采样…";
        _emptyHint.FontSize = 12;
        _emptyHint.Foreground = _labelBrush;
        _canvas.Children.Add(_emptyHint);

        Content = _canvas;
        SizeChanged += (_, _) => UpdateChart();
        Loaded += (_, _) =>
        {
            UpdateThemeBrushes();
            UpdateChart();
        };
        ActualThemeChanged += (_, _) =>
        {
            UpdateThemeBrushes();
            UpdateChart();
        };

        UpdateThemeBrushes();
    }

    private void UpdateThemeBrushes()
    {
        bool dark = ActualTheme == ElementTheme.Dark;
        _gridBrush.Color = dark ? Color.FromArgb(26, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0);
        _labelBrush.Color = dark ? Color.FromArgb(138, 255, 255, 255) : Color.FromArgb(138, 0, 0, 0);
        _thresholdBrush.Color = dark ? Color.FromArgb(255, 255, 193, 96) : Color.FromArgb(255, 194, 75, 0);
    }

    private void UpdateChart()
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        // 迷你趋势图不显示网格时去掉左侧刻度留白，让曲线铺满卡片宽度。
        double marginLeft = ShowGridLines ? MarginLeft : 4;
        double marginRight = ShowGridLines ? MarginRight : 4;
        double marginTop = ShowGridLines ? MarginTop : 4;
        double marginBottom = ShowGridLines ? MarginBottom : 2;

        double chartWidth = width - marginLeft - marginRight;
        double chartHeight = height - marginTop - marginBottom;
        if (chartWidth <= 0 || chartHeight <= 0)
            return;

        var clip = new RectangleGeometry { Rect = new Rect(marginLeft, marginTop, chartWidth, chartHeight) };
        _linePath.Clip = clip;
        _areaPath.Clip = clip;
        _endDot.Clip = null;

        for (int i = 0; i < GridLineCount; i++)
        {
            double y = marginTop + chartHeight * i / (GridLineCount - 1.0);
            var gridLine = _gridLines[i];
            gridLine.X1 = marginLeft;
            gridLine.Y1 = y;
            gridLine.X2 = marginLeft + chartWidth;
            gridLine.Y2 = y;
            gridLine.Visibility = ShowGridLines ? Visibility.Visible : Visibility.Collapsed;

            var label = _yLabels[i];
            label.Text = $"{100 - i * 25}%";
            label.Visibility = ShowGridLines ? Visibility.Visible : Visibility.Collapsed;
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, marginLeft - label.DesiredSize.Width - 6);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
        }

        var data = Data;
        int count = data?.Length ?? 0;
        bool hasData = count > 0;

        _emptyHint.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
        if (!hasData)
        {
            _emptyHint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(_emptyHint, marginLeft + Math.Max(0, (chartWidth - _emptyHint.DesiredSize.Width) / 2));
            Canvas.SetTop(_emptyHint, marginTop + Math.Max(0, (chartHeight - _emptyHint.DesiredSize.Height) / 2));
            _linePath.Data = null;
            _areaPath.Data = null;
            _endDot.Visibility = Visibility.Collapsed;
            _thresholdLine.Visibility = Visibility.Collapsed;
            _thresholdLabel.Visibility = Visibility.Collapsed;
            return;
        }

        // 阈值参考线
        double threshold = Math.Clamp(Threshold, 0, 100);
        double thresholdY = marginTop + chartHeight * (1.0 - threshold / 100.0);
        _thresholdLine.X1 = marginLeft;
        _thresholdLine.Y1 = thresholdY;
        _thresholdLine.X2 = marginLeft + chartWidth;
        _thresholdLine.Y2 = thresholdY;
        _thresholdLine.Visibility = ShowThresholdLine ? Visibility.Visible : Visibility.Collapsed;

        _thresholdLabel.Text = $"阈值 {threshold:0}%";
        _thresholdLabel.Visibility = ShowThresholdLine ? Visibility.Visible : Visibility.Collapsed;
        _thresholdLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(_thresholdLabel, marginLeft + chartWidth - _thresholdLabel.DesiredSize.Width);
        Canvas.SetTop(_thresholdLabel, thresholdY - _thresholdLabel.DesiredSize.Height - 1);

        int capacity = Math.Max(Capacity, 2);
        double stepX = chartWidth / (capacity - 1);
        int firstIndex = capacity - count;

        var points = new Point[count];
        for (int i = 0; i < count; i++)
        {
            double x = marginLeft + stepX * (firstIndex + i);
            double value = Math.Clamp(data![i], 0, 100);
            double y = marginTop + chartHeight * (1.0 - value / 100.0);
            points[i] = new Point(x, y);
        }

        var lineFigure = new PathFigure
        {
            StartPoint = points[0],
            IsClosed = false,
            IsFilled = false,
        };
        AppendSmoothSegments(lineFigure, points);
        var lineGeometry = new PathGeometry();
        lineGeometry.Figures.Add(lineFigure);
        _linePath.Data = lineGeometry;

        if (ShowAreaFill)
        {
            var areaFigure = new PathFigure
            {
                StartPoint = new Point(points[0].X, marginTop + chartHeight),
                IsClosed = true,
                IsFilled = true,
            };
            areaFigure.Segments.Add(new LineSegment { Point = points[0] });
            AppendSmoothSegments(areaFigure, points);
            areaFigure.Segments.Add(new LineSegment { Point = new Point(points[^1].X, marginTop + chartHeight) });
            var areaGeometry = new PathGeometry();
            areaGeometry.Figures.Add(areaFigure);
            _areaPath.Data = areaGeometry;
            _areaPath.Fill = CreateAreaBrush();
            _areaPath.Visibility = Visibility.Visible;
        }
        else
        {
            _areaPath.Visibility = Visibility.Collapsed;
        }

        _lineBrush.Color = AccentColor;
        _linePath.StrokeThickness = 2;
        _linePath.Visibility = Visibility.Visible;

        Canvas.SetLeft(_endDot, points[^1].X - 3);
        Canvas.SetTop(_endDot, points[^1].Y - 3);
        _endDot.Visibility = Visibility.Visible;
    }

    private Brush CreateAreaBrush()
    {
        var color = AccentColor;
        return new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops =
            {
                new GradientStop { Color = Color.FromArgb(64, color.R, color.G, color.B), Offset = 0 },
                new GradientStop { Color = Color.FromArgb(4, color.R, color.G, color.B), Offset = 1 },
            },
        };
    }

    /// <summary>使用 Catmull-Rom 样条把折线转换为平滑的三次贝塞尔曲线。</summary>
    private static void AppendSmoothSegments(PathFigure figure, Point[] points)
    {
        if (points.Length < 2)
            return;

        for (int i = 0; i < points.Length - 1; i++)
        {
            Point p0 = i == 0 ? points[0] : points[i - 1];
            Point p1 = points[i];
            Point p2 = points[i + 1];
            Point p3 = i + 2 < points.Length ? points[i + 2] : points[i + 1];

            var control1 = new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0);
            var control2 = new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0);

            figure.Segments.Add(new BezierSegment
            {
                Point1 = control1,
                Point2 = control2,
                Point3 = p2,
            });
        }
    }
}
