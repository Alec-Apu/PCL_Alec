using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PCL;

/// <summary>
/// Hover masks and press micro-animations for custom clickable surfaces.
/// The caller places <paramref name="mask" /> behind the content; this helper only animates it,
/// so the mask never intercepts hit testing.
/// </summary>
internal static class InteractionFeedback
{
    private const int HoverInDuration = 110;
    private const int HoverOutDuration = 200;
    private const int PressDuration = 90;
    private const int ReleaseDuration = 220;

    public static void Attach(FrameworkElement target, UIElement? mask = null,
        bool pressFeedback = true, double pressScale = 0.97d, double keyboardMaskOpacity = 0.5d,
        FrameworkElement? scaleTarget = null)
    {
        var pressedElement = scaleTarget ?? target;
        if (mask is not null)
        {
            mask.Opacity = 0d;
            target.MouseEnter += (_, _) => AnimateMask(target, mask, 1d, HoverInDuration);
            target.MouseLeave += (_, _) => AnimateMask(target, mask,
                target.IsKeyboardFocusWithin ? keyboardMaskOpacity : 0d, HoverOutDuration);
            target.IsKeyboardFocusWithinChanged += (_, _) =>
            {
                if (target.IsMouseOver)
                {
                    AnimateMask(target, mask, 1d, HoverInDuration);
                    return;
                }

                AnimateMask(target, mask, target.IsKeyboardFocusWithin ? keyboardMaskOpacity : 0d,
                    HoverOutDuration);
            };
        }

        if (!pressFeedback)
            return;
        EnsureScaleTransform(pressedElement);
        target.PreviewMouseLeftButtonDown += (_, _) =>
        {
            if (target.IsEnabled)
                AnimateScale(pressedElement, pressScale, PressDuration, true);
        };
        target.PreviewMouseLeftButtonUp += (_, _) => AnimateScale(pressedElement, 1d, ReleaseDuration, false);
        target.MouseLeave += (_, _) => AnimateScale(pressedElement, 1d, ReleaseDuration, false);
    }

    private static void EnsureScaleTransform(FrameworkElement target)
    {
        if (target.RenderTransform is ScaleTransform)
            return;
        target.RenderTransform = new ScaleTransform(1d, 1d);
        target.RenderTransformOrigin = new Point(0.5d, 0.5d);
    }

    private static void AnimateMask(FrameworkElement target, UIElement mask, double value, int time)
    {
        var key = "Interaction Mask " + target.GetHashCode();
        if (!ControlVisualHelpers.ShouldAnimate(target))
        {
            ModAnimation.AniStop(key);
            mask.Opacity = value;
            return;
        }

        ModAnimation.AniStart(
            ModAnimation.AaOpacity(mask, value - mask.Opacity, time,
                ease: new ModAnimation.AniEaseOutFluent()), key);
    }

    private static void AnimateScale(FrameworkElement target, double value, int time, bool isPress)
    {
        if (target.RenderTransform is not ScaleTransform scale)
            return;
        var key = "Interaction Scale " + target.GetHashCode();
        if (!ControlVisualHelpers.ShouldAnimate(target))
        {
            ModAnimation.AniStop(key);
            scale.ScaleX = value;
            scale.ScaleY = value;
            return;
        }

        var ease = isPress
            ? new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)
            : new ModAnimation.AniEaseOutFluent();
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(target, value - scale.ScaleX, time, isPress ? 0 : 10, ease), key);
    }
}
