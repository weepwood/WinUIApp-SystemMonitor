using System;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI.ViewManagement;

namespace WinUIApp.Controls;

/// <summary>
/// 统一的动效开关与时长约定。时长与缓动参考 Fluent 2 Motion：
/// 入场用减速曲线、短时长（150–300 ms），并始终尊重系统「显示动画」设置。
/// </summary>
public static class MotionSettings
{
    public const double QuickMs = 150;
    public const double NormalMs = 200;
    public const double GentleMs = 250;
    public const double SlowMs = 300;

    private static UISettings? _uiSettings;
    private static string _mode = "System";

    /// <summary>System 跟随系统 / Full 始终开启 / Off 关闭。</summary>
    public static void Configure(string mode)
    {
        _mode = mode is "Full" or "Off" ? mode : "System";
    }

    public static bool IsMotionEnabled => _mode switch
    {
        "Off" => false,
        "Full" => true,
        _ => SystemAnimationsEnabled,
    };

    private static bool SystemAnimationsEnabled
    {
        get
        {
            try
            {
                _uiSettings ??= new UISettings();
                return _uiSettings.AnimationsEnabled;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    public static void ResetPageEntrance(UIElement element)
    {
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            visual.StopAnimation("Opacity");
            visual.StopAnimation("Offset");
            visual.Opacity = 1f;
            visual.Offset = Vector3.Zero;
        }
        catch (Exception)
        {
            // 页面尚未进入视觉树时无需恢复。
        }
    }

    /// <summary>
    /// 页面切换时的入场动画：内容轻微上浮 + 淡入（Fluent 的 page refresh 观感）。
    /// 使用 Composition 而非 Storyboard，避免与布局系统争抢属性。
    /// </summary>
    public static void PlayPageEntrance(UIElement element, double offsetY = 12)
    {
        if (!IsMotionEnabled)
        {
            // 用户在播放中关闭动画时，也要清除之前动画留下的视觉状态。
            ResetPageEntrance(element);
            return;
        }

        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            // 减速曲线：快速起步、缓慢落位。
            var easing = compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.0f, 0.0f), new Vector2(0.1f, 1.0f));

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0f, 0f);
            opacity.InsertKeyFrame(1f, 1f, easing);
            opacity.Duration = TimeSpan.FromMilliseconds(NormalMs);
            visual.StartAnimation("Opacity", opacity);

            var offset = compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(0f, new Vector3(0f, (float)offsetY, 0f));
            offset.InsertKeyFrame(1f, Vector3.Zero, easing);
            offset.Duration = TimeSpan.FromMilliseconds(NormalMs);
            visual.StartAnimation("Offset", offset);
        }
        catch (Exception)
        {
            ResetPageEntrance(element);
        }
    }
}
