using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace PCL.Core.UI.Effects;

/// <summary>Background-only rounded lens, adapted from AndroidLiquidGlass (Apache-2.0).</summary>
public sealed class LiquidGlassEffect : ShaderEffect
{
    private static readonly PixelShader Shader = new()
    {
        UriSource = new Uri("pack://application:,,,/PCL.Core;component/UI/Assets/Shaders/LiquidGlass.ps")
    };

    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty(nameof(Input), typeof(LiquidGlassEffect), 0);
    // Point4D preserves signed and unbounded float4 shader parameters.
    private static DependencyProperty Constant(string name, int register) => DependencyProperty.Register(name, typeof(Point4D), typeof(LiquidGlassEffect), new UIPropertyMetadata(new Point4D(), PixelShaderConstantCallback(register)));
    public static readonly DependencyProperty SizeConstantProperty = Constant(nameof(SizeConstant), 0);
    public static readonly DependencyProperty CornersProperty = Constant(nameof(Corners), 1);
    public static readonly DependencyProperty LensProperty = Constant(nameof(Lens), 2);
    public static readonly DependencyProperty LightProperty = Constant(nameof(Light), 3);
    public static readonly DependencyProperty TintProperty = Constant(nameof(Tint), 4);
    public static readonly DependencyProperty ControlsProperty=Constant(nameof(Controls),5);
    public static readonly DependencyProperty OverlayTintProperty=Constant(nameof(OverlayTint),6);
    public static readonly DependencyProperty InteractionProperty=Constant(nameof(Interaction),7);
    public static readonly DependencyProperty TabsProperty=RegisterPixelShaderSamplerProperty(nameof(Tabs),typeof(LiquidGlassEffect),1);

    public LiquidGlassEffect()
    {
        PixelShader = Shader;
        foreach (var property in new[] { InputProperty, SizeConstantProperty, CornersProperty, LensProperty, LightProperty, TintProperty, ControlsProperty, OverlayTintProperty, InteractionProperty, TabsProperty }) UpdateShaderValue(property);
    }

    public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty, value); }
    public Point4D SizeConstant { get => (Point4D)GetValue(SizeConstantProperty); set => SetValue(SizeConstantProperty, value); }
    public Point4D Corners { get => (Point4D)GetValue(CornersProperty); set => SetValue(CornersProperty, value); }
    public Point4D Lens { get => (Point4D)GetValue(LensProperty); set => SetValue(LensProperty, value); }
    public Point4D Light { get => (Point4D)GetValue(LightProperty); set => SetValue(LightProperty, value); }
    public Point4D Tint { get => (Point4D)GetValue(TintProperty); set => SetValue(TintProperty, value); }
    public Point4D Controls { get => (Point4D)GetValue(ControlsProperty); set => SetValue(ControlsProperty,value); }
    public Point4D OverlayTint { get => (Point4D)GetValue(OverlayTintProperty); set => SetValue(OverlayTintProperty,value); }
    public Point4D Interaction { get => (Point4D)GetValue(InteractionProperty); set => SetValue(InteractionProperty,value); }
    public Brush Tabs { get => (Brush)GetValue(TabsProperty); set => SetValue(TabsProperty,value); }
    public static Point4D Vector(double x, double y, double z, double w) => new(x, y, z, w);
}
