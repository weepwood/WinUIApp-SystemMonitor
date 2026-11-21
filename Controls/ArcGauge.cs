using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace WinUIApp.Controls;

/// <summary>
/// 270° 弧形仪表盘。轨道使用主题感知的半透明画刷，数值弧使用强调色圆头描边，
/// 中央显示大号数值与说明文字。
/// </summary>
public sealed class ArcGauge : UserControl
{
    private const double StartAngleDeg = 135.0;
    private const double SweepAngleDeg = 270.0;

    private readonly Grid _rootGrid = new();
    private readonly Path _trackArc = new();
    private readonly Path _valueArc = new();
    private readonly TextBlock _valueText = new();
    private readonly TextBlock _captionText = new();
    private readonly SolidColorBrush _trackBrush = new();
    private readonly SolidColorBrush _valueBrush = new();

    private double _displayedFraction;
    private double _targetFraction;
    private double _lastArcFraction = -1;
    private double _lastRenderedDimension = -1;
    private double _lastStrokeWidth = -1;
    private Color _lastGaugeColor;
    private bool _hasRenderedColor;
    private double _animationFrom;
    private TimeSpan _animationStart;
    private bool _isAnimating;

    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent), typeof(float), typeof(ArcGauge),
        new PropertyMetadata(0f, OnPercentChanged));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(ArcGauge),
        new PropertyMetadata("0%", OnValueChanged));

    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(ArcGauge),
        new PropertyMetadata(string.Empty, OnCaptionChanged));

    public static readonly DependencyProperty GaugeColorProperty = DependencyProperty.Register(
        nameof(GaugeColor), typeof(Color), typeof(ArcGauge),
        new PropertyMetadata(Color.FromArgb(255, 96, 205, 255), OnVisualChanged));

    public static readonly DependencyProperty StrokeWidthProperty = DependencyProperty.Register(
        nameof(StrokeWidth), typeof(double), typeof(ArcGauge),
        new PropertyMetadata(16.0, OnVisualChanged));

    /// <summary>0-100 的填充比例。</summary>
    public float Percent
    {
        get => (float)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
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

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGauge gauge)
            gauge.UpdateGauge();
    }

    private static void OnPercentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGauge gauge)
            gauge.AnimateToTarget();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGauge gauge)
            gauge._valueText.Text = e.NewValue as string ?? string.Empty;
    }

    private static void OnCaptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGauge gauge)
            gauge._captionText.Text = e.NewValue as string ?? string.Empty;
    }

    public ArcGauge()
    {
        _trackArc.Stroke = _trackBrush;
        _trackArc.StrokeStartLineCap = PenLineCap.Round;
        _trackArc.StrokeEndLineCap = PenLineCap.Round;

        _valueArc.Stroke = _valueBrush;
        _valueArc.StrokeStartLineCap = PenLineCap.Round;
        _valueArc.StrokeEndLineCap = PenLineCap.Round;

        _valueText.HorizontalAlignment = HorizontalAlignment.Center;
        _valueText.VerticalAlignment = VerticalAlignment.Center;
        _valueText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        _valueText.Text = "0%";

        _captionText.HorizontalAlignment = HorizontalAlignment.Center;
        _captionText.VerticalAlignment = VerticalAlignment.Center;
        _captionText.Opacity = 0.7;

        _rootGrid.Children.Add(_trackArc);
        _rootGrid.Children.Add(_valueArc);
        _rootGrid.Children.Add(_valueText);
        _rootGrid.Children.Add(_captionText);

        Content = _rootGrid;
        SizeChanged += (_, _) => UpdateGauge();
        Loaded += (_, _) =>
        {
            UpdateThemeBrushes();
            UpdateGauge();
        };
        Unloaded += (_, _) => StopAnimation();
        ActualThemeChanged += (_, _) =>
        {
            UpdateThemeBrushes();
            UpdateGauge();
        };

        UpdateThemeBrushes();
    }

    private void UpdateThemeBrushes()
    {
        bool dark = ActualTheme == ElementTheme.Dark;
        _trackBrush.Color = dark ? Color.FromArgb(46, 255, 255, 255) : Color.FromArgb(31, 0, 0, 0);
    }

    /// <summary>
    /// 把填充比例平滑过渡到目标值：300 ms 减速曲线，节拍内不产生跳变。
    /// 系统或用户关闭动画时直接落位，不做任何过渡。
    /// </summary>
    private void AnimateToTarget()
    {
        _targetFraction = Math.Clamp(Percent / 100.0, 0.0, 1.0);

        if (!MotionSettings.IsMotionEnabled)
        {
            StopAnimation();
            _displayedFraction = _targetFraction;
            UpdateGauge();
            return;
        }

        _animationFrom = _displayedFraction;
        _animationStart = TimeSpan.MinValue;

        if (!_isAnimating)
        {
            _isAnimating = true;
            CompositionTarget.Rendering += OnRendering;
        }
    }

    private void OnRendering(object? sender, object e)
    {
        TimeSpan now = e is RenderingEventArgs args ? args.RenderingTime : TimeSpan.Zero;
        if (_animationStart == TimeSpan.MinValue)
            _animationStart = now;

        double elapsed = (now - _animationStart).TotalMilliseconds;
        double progress = Math.Clamp(elapsed / MotionSettings.SlowMs, 0.0, 1.0);
        double eased = 1.0 - Math.Pow(1.0 - progress, 3.0);

        _displayedFraction = _animationFrom + (_targetFraction - _animationFrom) * eased;
        UpdateGauge();

        if (progress >= 1.0)
        {
            _displayedFraction = _targetFraction;
            StopAnimation();
            UpdateGauge();
        }
    }

    private void StopAnimation()
    {
        if (!_isAnimating)
            return;

        CompositionTarget.Rendering -= OnRendering;
        _isAnimating = false;
    }

    private void UpdateGauge()
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        double minDim = Math.Min(width, height);
        double strokeWidth = StrokeWidth > 0 ? StrokeWidth : Math.Max(10, minDim * 0.07);
        strokeWidth = Math.Min(strokeWidth, minDim * 0.22);
        double radius = (minDim - strokeWidth) / 2.0;
        if (radius <= 0)
            return;

        double centerX = width / 2.0;
        double centerY = height / 2.0;

        double startAngleRad = StartAngleDeg * Math.PI / 180.0;
        double sweepAngleRad = SweepAngleDeg * Math.PI / 180.0;

        // 尺寸、线宽与颜色只在实际变化时写入：动画期间每帧重复设置字号与边距
        // 会让文本反复测量、布局，这正是原先仪表盘动画掉帧的原因。
        bool geometryDirty = Math.Abs(minDim - _lastRenderedDimension) > 0.5
            || Math.Abs(strokeWidth - _lastStrokeWidth) > 0.25;

        if (geometryDirty)
        {
            _lastRenderedDimension = minDim;
            _lastStrokeWidth = strokeWidth;

            _trackArc.Data = CreateArcGeometry(centerX, centerY, radius, startAngleRad, sweepAngleRad);
            _trackArc.StrokeThickness = strokeWidth;
            _valueArc.StrokeThickness = strokeWidth;

            _valueText.FontSize = Math.Max(16, minDim * 0.17);
            _captionText.FontSize = Math.Max(10, minDim * 0.062);
            _valueText.Margin = new Thickness(0, -minDim * 0.035, 0, 0);
            _captionText.Margin = new Thickness(0, minDim * 0.115, 0, 0);

            // 半径变化后必须重建数值弧，否则会继续沿用旧尺寸的几何。
            _lastArcFraction = -1;
        }

        if (!_hasRenderedColor || GaugeColor != _lastGaugeColor)
        {
            _lastGaugeColor = GaugeColor;
            _hasRenderedColor = true;
            _valueBrush.Color = GaugeColor;
        }

        // 数值弧按 0.2% 的粒度重建：更细的变化在屏幕上不可见，却要多建一个几何。
        double pct = _displayedFraction;
        bool empty = pct <= 0.0001;
        bool unchanged = Math.Abs(pct - _lastArcFraction) < 0.002
            && (empty ? _valueArc.Data is null : _valueArc.Data is not null);
        if (unchanged)
            return;

        _lastArcFraction = pct;
        double fillSweep = sweepAngleRad * pct;
        _valueArc.Data = empty
            ? null
            : CreateArcGeometry(centerX, centerY, radius, startAngleRad, fillSweep);
    }

    private static PathGeometry CreateArcGeometry(double cx, double cy, double radius, double startAngleRad, double sweepAngleRad)
    {
        double endAngleRad = startAngleRad + sweepAngleRad;

        double x1 = cx + radius * Math.Cos(startAngleRad);
        double y1 = cy + radius * Math.Sin(startAngleRad);
        double x2 = cx + radius * Math.Cos(endAngleRad);
        double y2 = cy + radius * Math.Sin(endAngleRad);

        var figure = new PathFigure
        {
            StartPoint = new Windows.Foundation.Point(x1, y1),
            IsClosed = false,
        };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Windows.Foundation.Point(x2, y2),
            Size = new Windows.Foundation.Size(radius, radius),
            IsLargeArc = sweepAngleRad > Math.PI,
            SweepDirection = SweepDirection.Clockwise,
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
