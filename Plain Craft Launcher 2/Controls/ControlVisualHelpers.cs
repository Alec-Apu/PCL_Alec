using System.Windows;

namespace PCL;

internal static class ControlVisualHelpers
{
    internal static bool ShouldAnimate(FrameworkElement control, object? animationOverride = null)
    {
        return SystemParameters.ClientAreaAnimation && control.IsLoaded && control.IsVisible && ModAnimation.AniControlEnabled == 0 && !false.Equals(animationOverride);
    }

    internal static bool IsInViewport(FrameworkElement element, FrameworkElement viewport)
    {
        if (!element.IsLoaded || !element.IsVisible || viewport.ActualWidth <= 0 || viewport.ActualHeight <= 0) return false;
        return element.TransformToAncestor(viewport).TransformBounds(new Rect(element.RenderSize))
            .IntersectsWith(new Rect(viewport.RenderSize));
    }

    internal static void AnimateColorOrSetResource(FrameworkElement target, DependencyProperty property,
        string resourceKey, int duration, string animationKey, bool shouldAnimate)
    {
        if (shouldAnimate)
        {
            ModAnimation.AniStart(ModAnimation.AaColor(target, property, resourceKey, duration), animationKey);
        }
        else
        {
            ModAnimation.AniStop(animationKey);
            target.SetResourceReference(property, resourceKey);
        }
    }
}
