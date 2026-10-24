using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using PCL.Core.UI.Theme;

namespace PCL;

/// <summary>Material tokens follow the existing palette and Windows accessibility preferences.</summary>
internal static class FluentTheme
{
    private static bool _initialized;
    private static bool _wasHighContrast;

    public static void Refresh()
    {
        var app = System.Windows.Application.Current;
        if (app is null) return;
        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(Refresh);
            return;
        }
        if (!_initialized)
        {
            _initialized = true;
            SystemParameters.StaticPropertyChanged += OnSystemPreferenceChanged;
            SystemEvents.UserPreferenceChanged += (_, _) => app.Dispatcher.BeginInvoke(Refresh);
        }
        var transparent = !FormMain.UseClassicLayout && !SystemParameters.HighContrast && RenderCapability.Tier >= 0x10000;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            transparent &= (key?.GetValue("EnableTransparency") as int? ?? 1) != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        { transparent = false; }
        if (_wasHighContrast && !SystemParameters.HighContrast)
        {
            ThemeService.ApplyGrayResources();
            ThemeService.ApplyColorResources();
        }
        _wasHighContrast = SystemParameters.HighContrast;
        var dark = ThemeService.IsDarkMode;
        SetBrush("Fluent.TitleBar", dark ? Colors.Black : Colors.White);
        SetBrush("Fluent.NavigationHover", dark ? Color.FromArgb(58,255,255,255) : Color.FromArgb(76,255,255,255));
        SetBrush("Fluent.NavigationPressed", dark ? Color.FromArgb(86,255,255,255) : Color.FromArgb(112,255,255,255));
        // Neutral interaction washes survive the lightweight material's alpha clamp.
        var highlight = SystemColors.HighlightColor;
        SetBrush("Fluent.ActionHover", SystemParameters.HighContrast
            ? Color.FromArgb(40, highlight.R, highlight.G, highlight.B)
            : dark ? Color.FromArgb(28,255,255,255) : Color.FromArgb(20,0,0,0));
        SetBrush("Fluent.ActionPressed", SystemParameters.HighContrast
            ? Color.FromArgb(72, highlight.R, highlight.G, highlight.B)
            : dark ? Color.FromArgb(51,255,255,255) : Color.FromArgb(41,0,0,0));
        if (app.Resources["ColorBrush3"] is SolidColorBrush accent)
        {
            var color = accent.Color;
            static double Linear(byte channel)
            {
                var value = channel / 255d;
                return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            }
            static Color Scale(Color source, double factor) => Color.FromRgb(
                (byte)Math.Round(source.R * factor), (byte)Math.Round(source.G * factor),
                (byte)Math.Round(source.B * factor));
            SetBrush("Fluent.AccentFill", dark ? Scale(color, 0.72d) : color);
            SetBrush("Fluent.AccentHover", dark ? Scale(color, 0.60d) : Scale(color, 0.82d));
            SetBrush("Fluent.OnAccent", dark || 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B) <= 0.179
                ? Colors.White : Colors.Black);
        }
        if (SystemParameters.HighContrast)
        {
            SetBrush("ColorBrush1", SystemColors.WindowTextColor);
            SetBrush("ColorBrush3", SystemColors.HighlightColor);
            SetBrush("Fluent.AccentFill", SystemColors.HighlightColor);
            SetBrush("Fluent.AccentHover", SystemColors.HighlightColor);
            SetBrush("Fluent.OnAccent", SystemColors.HighlightTextColor);
        }
        // Neutral carriers preserve wallpaper colors; the shader owns the tint ceiling.
        var surfaceColor = dark ? Color.FromRgb(24,24,26) : Colors.White;
        Color Surface(byte opacity) => Color.FromArgb(transparent ? opacity : (byte)255,surfaceColor.R,surfaceColor.G,surfaceColor.B);
        SetBrush("Fluent.Surface", SystemParameters.HighContrast ? SystemColors.WindowColor : Surface(dark ? (byte)133 : (byte)122));
        SetBrush("Fluent.GlassSurface", SystemParameters.HighContrast ? SystemColors.WindowColor : Surface(dark ? (byte)76 : (byte)51));
        SetBrush("Fluent.Card", SystemParameters.HighContrast ? SystemColors.WindowColor : Surface(dark ? (byte)128 : (byte)122));
        SetBrush("Fluent.Stroke", SystemParameters.HighContrast ? SystemColors.WindowTextColor :
            Color.FromArgb(dark ? (byte)38 : (byte)24, dark ? (byte)255 : (byte)0,
                dark ? (byte)255 : (byte)0, dark ? (byte)255 : (byte)0));
        app.Resources["Fluent.BlurRadius"] = transparent && PCL.Core.App.Config.Preference.Blur.IsEnabled
            ? Math.Clamp(PCL.Core.App.Config.Preference.Blur.Radius, 0, 64) * 1d : 0d;
        app.Resources["LiquidGlass.Enabled"] = transparent && RenderCapability.IsPixelShaderVersionSupported(3, 0);
        app.Resources["LiquidGlass.BlurRadius"] = transparent && PCL.Core.App.Config.Preference.Blur.IsEnabled
            ? Math.Clamp(PCL.Core.App.Config.Preference.Blur.Radius, 0, 32) * 1d : 0d;
        app.Resources["LiquidGlass.BlurKernelType"] = (System.Windows.Media.Effects.KernelType)Math.Clamp(PCL.Core.App.Config.Preference.Blur.KernelType, 0, 1);
        SetBrush("LiquidGlass.Fallback", SystemParameters.HighContrast ? SystemColors.WindowColor : dark ? Color.FromRgb(29, 33, 43) : Color.FromRgb(244, 247, 252));
        var backdrop = new LinearGradientBrush();
        backdrop.StartPoint = new Point(0, 0);
        backdrop.EndPoint = new Point(1, 1);
        backdrop.GradientStops.Add(new GradientStop(dark ? Color.FromRgb(20, 31, 49) : Color.FromRgb(204, 226, 244), 0));
        backdrop.GradientStops.Add(new GradientStop(dark ? Color.FromRgb(43, 37, 63) : Color.FromRgb(231, 222, 246), .48));
        backdrop.GradientStops.Add(new GradientStop(dark ? Color.FromRgb(22, 43, 53) : Color.FromRgb(216, 239, 235), 1));
        backdrop.Freeze();
        app.Resources["LiquidGlass.Backdrop"] = SystemParameters.HighContrast ? SystemColors.WindowBrush : backdrop;
        SetBrush("ColorBrushGray2", SystemParameters.HighContrast ? SystemColors.WindowTextColor : dark ? Color.FromRgb(184,197,212) : Color.FromRgb(76,88,106));
    }

    private static void OnSystemPreferenceChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private static void SetBrush(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        System.Windows.Application.Current.Resources[key] = brush;
    }
}
