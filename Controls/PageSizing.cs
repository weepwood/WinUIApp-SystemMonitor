using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUIApp.Controls;

/// <summary>
/// 页面留白的自适应：宽屏 24 px，窄屏收到 16 px 甚至 12 px。
/// 固定 24 px 在 600 px 宽的窗口里会吃掉近一成的可用宽度，
/// 而这一圈留白在小窗口里几乎不提供任何视觉价值。
/// </summary>
public static class PageSizing
{
    /// <summary>低于此宽度时使用紧凑留白（与双栏堆叠断点一致）。</summary>
    public const double CompactWidth = Breakpoints.Stack;

    /// <summary>低于此宽度时使用最紧凑留白。</summary>
    public const double TightWidth = 440;

    /// <summary>为页面内容容器挂上按宽度变化的留白。</summary>
    public static void Attach(FrameworkElement source, Panel content)
    {
        if (source is null || content is null)
            return;

        source.SizeChanged += (_, e) => Apply(content, e.NewSize.Width);
        content.Loaded += (_, _) => Apply(content, source.ActualWidth);
    }

    private static void Apply(Panel content, double width)
    {
        if (!double.IsFinite(width) || width <= 0)
            return;

        var target = width < TightWidth
            ? new Thickness(12, 6, 12, 24)
            : width < CompactWidth
                ? new Thickness(16, 8, 16, 28)
                : new Thickness(24, 8, 24, 32);

        var current = content.Padding;
        if (current.Left != target.Left || current.Top != target.Top
            || current.Right != target.Right || current.Bottom != target.Bottom)
        {
            content.Padding = target;
        }
    }
}
