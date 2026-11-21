using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUIApp.Controls;

/// <summary>
/// 全应用共用的宽度断点。集中在一处，避免每个页面各写一套魔数：
/// 以前总览用 820/900/1440、内存与系统信息用 900，而实际可用内容宽度还要减去
/// 导航栏和 48 px 页边距，导致同一窗口宽度下各页判断不一致。
/// </summary>
public static class Breakpoints
{
    /// <summary>内容区宽度小于此值时，左右并排的双栏改为上下堆叠。</summary>
    public const double Stack = 720;

    /// <summary>内容区宽度小于此值时，依次折叠统计磁贴、压缩指标卡内边距。</summary>
    public const double Compact = 560;

    /// <summary>内容区宽度小于此值时，命令条改为「主操作 + 溢出菜单」。</summary>
    public const double Collapse = 620;

    /// <summary>
    /// 布局判定用的宽度：优先使用扣除页面留白后的内容宽度，
    /// 这样断点语义与用户看到的「卡片实际宽度」一致。
    /// </summary>
    public static double ContentWidth(FrameworkElement element)
    {
        double width = element.ActualWidth;
        if (!double.IsFinite(width) || width <= 0)
            return 0;

        // FrameworkElement 与 Panel 都没有 Padding：
        // 只有 Control、Border 以及 WinUI 3 的 StackPanel 提供该属性。
        Thickness padding = element switch
        {
            Control control => control.Padding,
            Border border => border.Padding,
            StackPanel stack => stack.Padding,
            _ => default,
        };

        double content = width - padding.Left - padding.Right;
        return content > 0 ? content : width;
    }

    /// <summary>内容宽度对应的最大卡片列数（用于一次列出指标卡等固定项集合）。</summary>
    public static int ColumnCount(double contentWidth, double minItemWidth)
    {
        if (!double.IsFinite(contentWidth) || contentWidth <= 0 || minItemWidth <= 0)
            return 1;

        return Math.Clamp((int)Math.Floor(contentWidth / minItemWidth), 1, 4);
    }
}
