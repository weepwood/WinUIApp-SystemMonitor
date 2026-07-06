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
/// CPU 历史使用率折线图控件，使用 WinUI Canvas 和 Polyline 绘制
/// </summary>
public sealed class CpuHistoryChart : UserControl
{
    private readonly Canvas _drawingCanvas;
    private readonly Polyline _dataLine;
    private readonly Polyline _fillPolygon;
    private readonly TextBlock[] _yLabels = new TextBlock[5];
    private readonly Rectangle _borderRect;

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(float[]), typeof(CpuHistoryChart),
            new PropertyMetadata(Array.Empty<float>(), OnDataChanged));

    public static readonly DependencyProperty DataCountProperty =
        DependencyProperty.Register(nameof(DataCount), typeof(int), typeof(CpuHistoryChart),
            new PropertyMetadata(0, OnDataChanged));

    public static readonly DependencyProperty LineColorProperty =
        DependencyProperty.Register(nameof(LineColor), typeof(Color), typeof(CpuHistoryChart),
            new PropertyMetadata(Colors.DodgerBlue, OnDataChanged));

    public float[] Data
    {
        get => (float[])GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public int DataCount
    {
        get => (int)GetValue(DataCountProperty);
        set => SetValue(DataCountProperty, value);
    }

    public Color LineColor
    {
        get => (Color)GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CpuHistoryChart chart)
            chart.UpdateChart();
    }

    public CpuHistoryChart()
    {
        _drawingCanvas = new Canvas();

        // 背景网格线
        for (int i = 0; i < 5; i++)
        {
            var gridLine = new Line
            {
                Stroke = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                StrokeThickness = 1,
                X1 = 0, X2 = 0,
                Y1 = 0, Y2 = 0,
            };
            _drawingCanvas.Children.Add(gridLine);
        }

        // 边框
        _borderRect = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            StrokeThickness = 1,
            Fill = new SolidColorBrush(Colors.Transparent),
        };
        _drawingCanvas.Children.Add(_borderRect);

        // 填充多边形
        _fillPolygon = new Polyline
        {
            Fill = new SolidColorBrush(Color.FromArgb(30, 30, 144, 255)),
            StrokeThickness = 0,
        };
        _drawingCanvas.Children.Add(_fillPolygon);

        // 数据折线
        _dataLine = new Polyline
        {
            Stroke = new SolidColorBrush(Colors.DodgerBlue),
            StrokeThickness = 2,
        };
        _drawingCanvas.Children.Add(_dataLine);

        // Y 轴标签
        for (int i = 0; i < 5; i++)
        {
            var label = new TextBlock
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.Gray),
                Text = $"{100 - i * 25}%",
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            _yLabels[i] = label;
            _drawingCanvas.Children.Add(label);
        }

        Content = _drawingCanvas;
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateChart();
    }

    private void UpdateChart()
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        double marginLeft = 38;
        double marginRight = 8;
        double marginTop = 16;
        double marginBottom = 28;

        double chartWidth = width - marginLeft - marginRight;
        double chartHeight = height - marginTop - marginBottom;

        if (chartWidth <= 0 || chartHeight <= 0) return;

        // 更新边框
        _borderRect.Width = chartWidth;
        _borderRect.Height = chartHeight;
        Canvas.SetLeft(_borderRect, marginLeft);
        Canvas.SetTop(_borderRect, marginTop);

        // 更新网格线和 Y 轴标签
        int gridLineIndex = 0;
        for (int i = 0; i < 5; i++)
        {
            double y = marginTop + chartHeight * i / 4.0;
            var gridLine = (Line)_drawingCanvas.Children[gridLineIndex];
            gridLine.X1 = marginLeft;
            gridLine.Y1 = y;
            gridLine.X2 = width - marginRight;
            gridLine.Y2 = y;
            gridLineIndex++;

            _yLabels[i].Text = $"{100 - i * 25}%";
            _yLabels[i].Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(_yLabels[i], marginLeft - _yLabels[i].DesiredSize.Width - 4);
            Canvas.SetTop(_yLabels[i], y - _yLabels[i].DesiredSize.Height / 2);
        }

        // 更新数据
        var data = Data;
        int count = Math.Min(DataCount, data?.Length ?? 0);

        if (count < 1 || data == null)
        {
            _dataLine.Points = new PointCollection();
            _fillPolygon.Points = new PointCollection();
            return;
        }

        double maxValue = 100.0;
        var linePoints = new PointCollection();
        var fillPoints = new PointCollection();

        // 底部左边起点
        fillPoints.Add(new Point(marginLeft, marginTop + chartHeight));

        for (int i = 0; i < count; i++)
        {
            double x = marginLeft + chartWidth * i / 59.0;
            double y = marginTop + chartHeight * (1.0 - Math.Clamp(data[i], 0, maxValue) / maxValue);
            var point = new Point(x, y);
            linePoints.Add(point);
            fillPoints.Add(point);
        }

        // 底部右边终点
        fillPoints.Add(new Point(linePoints[^1].X, marginTop + chartHeight));

        _dataLine.Points = linePoints;
        _fillPolygon.Points = fillPoints;

        _dataLine.Stroke = new SolidColorBrush(LineColor);
        _fillPolygon.Fill = new SolidColorBrush(Color.FromArgb(30, LineColor.R, LineColor.G, LineColor.B));
    }
}
