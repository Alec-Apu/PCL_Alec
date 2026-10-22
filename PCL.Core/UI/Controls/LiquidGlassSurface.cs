using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using PCL.Core.UI.Effects;

namespace PCL.Core.UI.Controls;

public enum LiquidGlassMaterial { Content, Chrome, Action, Input, Floating, BottomTabs, BottomTabIndicator }

/// <summary>
/// A background-only glass border. The source must be a separate visual behind the UI,
/// never an ancestor containing this surface. Foreground children remain native WPF visuals.
/// </summary>
public class LiquidGlassSurface : Border
{
    private static readonly ImageBrush EmptyTabs=CreateEmptyTabs();
    private static ImageBrush CreateEmptyTabs()
    {
        var pixel=BitmapSource.Create(1,1,96,96,PixelFormats.Pbgra32,null,new byte[4],4);pixel.Freeze();
        var brush=new ImageBrush(pixel);brush.Freeze();return brush;
    }
    public static readonly DependencyProperty BackdropSourceProperty = DependencyProperty.RegisterAttached(
        "BackdropSource", typeof(FrameworkElement), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
    public static void SetBackdropSource(DependencyObject target, FrameworkElement? value) => target.SetValue(BackdropSourceProperty, value);
    public static FrameworkElement? GetBackdropSource(DependencyObject target) => (FrameworkElement?)target.GetValue(BackdropSourceProperty);

    // Opt-in reduced material for ordinary page surfaces. The two dock materials
    // never honor it, so their live scene sampling and optical pipeline stay intact.
    public static readonly DependencyProperty UseLightweightMaterialProperty = DependencyProperty.RegisterAttached(
        "UseLightweightMaterial", typeof(bool), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender,
            (target, _) => { if (target is LiquidGlassSurface surface) surface.UpdateCoordinateTracking(); }));
    public static void SetUseLightweightMaterial(DependencyObject target, bool value) => target.SetValue(UseLightweightMaterialProperty, value);
    public static bool GetUseLightweightMaterial(DependencyObject target) => (bool)target.GetValue(UseLightweightMaterialProperty);

    public static readonly DependencyProperty MaterialProperty = DependencyProperty.Register(nameof(Material), typeof(LiquidGlassMaterial), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(LiquidGlassMaterial.Content, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.Register(nameof(BlurRadius), typeof(double), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsRender), value => value is double d && double.IsFinite(d) && d >= 0 && d <= 64);
    public static readonly DependencyProperty GlassEnabledProperty = DependencyProperty.Register(nameof(GlassEnabled), typeof(bool), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty BlurKernelTypeProperty = DependencyProperty.Register(nameof(BlurKernelType), typeof(KernelType), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(KernelType.Gaussian, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FallbackBrushProperty = DependencyProperty.Register(nameof(FallbackBrush), typeof(Brush), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(Brushes.WhiteSmoke, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty RefractionStrengthProperty = DependencyProperty.Register(nameof(RefractionStrength), typeof(double), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender), value => value is double d && double.IsFinite(d) && d >= 0 && d <= 2);
    private static DependencyProperty RenderProperty(string name,Type type,object value) => DependencyProperty.Register(name,type,typeof(LiquidGlassSurface),new FrameworkPropertyMetadata(value,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PressProgressProperty=RenderProperty(nameof(PressProgress),typeof(double),0d);
    public static readonly DependencyProperty InteractionProgressProperty=RenderProperty(nameof(InteractionProgress),typeof(double),0d);
    public static readonly DependencyProperty InteractionPositionProperty=RenderProperty(nameof(InteractionPosition),typeof(Point),new Point());
    public static readonly DependencyProperty TabOverlayProperty=RenderProperty(nameof(TabOverlay),typeof(Brush),EmptyTabs);

    public LiquidGlassMaterial Material { get => (LiquidGlassMaterial)GetValue(MaterialProperty); set => SetValue(MaterialProperty, value); }
    public double BlurRadius { get => (double)GetValue(BlurRadiusProperty); set => SetValue(BlurRadiusProperty, value); }
    public bool GlassEnabled { get => (bool)GetValue(GlassEnabledProperty); set => SetValue(GlassEnabledProperty, value); }
    public KernelType BlurKernelType { get => (KernelType)GetValue(BlurKernelTypeProperty); set => SetValue(BlurKernelTypeProperty, value); }
    public Brush FallbackBrush { get => (Brush)GetValue(FallbackBrushProperty); set => SetValue(FallbackBrushProperty, value); }
    public double RefractionStrength { get => (double)GetValue(RefractionStrengthProperty); set => SetValue(RefractionStrengthProperty, value); }
    public double PressProgress { get => (double)GetValue(PressProgressProperty); set => SetValue(PressProgressProperty,value); }
    public double InteractionProgress { get => (double)GetValue(InteractionProgressProperty); set => SetValue(InteractionProgressProperty,value); }
    public Point InteractionPosition { get => (Point)GetValue(InteractionPositionProperty); set => SetValue(InteractionPositionProperty,value); }
    public Brush TabOverlay { get => (Brush)GetValue(TabOverlayProperty); set => SetValue(TabOverlayProperty,value); }

    private FrameworkElement? _source;
    private Rect _sourceRect;
    private Size _sourceSize;
    private DrawingVisual? _sample, _lens;
    private VisualBrush? _output;
    private bool _checking;
    private bool _isLoaded;
    private bool _coordinateTracking;
    // Retain the render graph: animated constants must not recreate effect resources.
    private VisualBrush? _backdrop, _sampleBrush;
    private BlurEffect? _blur;
    private LiquidGlassEffect? _effect;
    private Rect _expanded;
    private Brush? _sampleFallback;
    private Geometry? _clip;
    private Size _clipSize;
    private CornerRadius _clipCorners;

    public LiquidGlassSurface()
    {
        SetResourceReference(GlassEnabledProperty, "LiquidGlass.Enabled");
        SetResourceReference(FallbackBrushProperty, "LiquidGlass.Fallback");
        SetResourceReference(BlurRadiusProperty, "LiquidGlass.BlurRadius");
        SetResourceReference(BlurKernelTypeProperty, "LiquidGlass.BlurKernelType");
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        UpdateCoordinateTracking();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        StopCoordinateTracking();
        ReleaseRenderGraph();
        _clip = null;
        _clipSize = default;
        _clipCorners = default;
    }

    private bool IsDockMaterial => Material is LiquidGlassMaterial.BottomTabs or LiquidGlassMaterial.BottomTabIndicator;

    private bool RequestsLightweightMaterial =>
        GetUseLightweightMaterial(this) && Material is LiquidGlassMaterial.Content or LiquidGlassMaterial.Chrome
            or LiquidGlassMaterial.Action or LiquidGlassMaterial.Input;

    private bool UsesLightweightMaterial =>
        RequestsLightweightMaterial && GlassEnabled && !SystemParameters.HighContrast
        && RenderCapability.IsPixelShaderVersionSupported(3, 0);

    private bool ShouldTrackCoordinates => Background is not null && (IsDockMaterial || !RequestsLightweightMaterial);

    private void UpdateCoordinateTracking()
    {
        if (!_isLoaded) return;
        var shouldTrack = ShouldTrackCoordinates;
        if (shouldTrack == _coordinateTracking)
        {
            if (!shouldTrack && _source is not null)
            {
                _source = null;
                InvalidateVisual();
            }
            return;
        }
        _coordinateTracking = shouldTrack;
        if (shouldTrack)
        {
            LayoutUpdated += OnLayoutUpdated;
            RefreshCoordinates();
        }
        else
        {
            LayoutUpdated -= OnLayoutUpdated;
            ReleaseRenderGraph();
            InvalidateVisual();
        }
    }

    private void StopCoordinateTracking()
    {
        if (_coordinateTracking) LayoutUpdated -= OnLayoutUpdated;
        _coordinateTracking = false;
    }

    private void ReleaseRenderGraph()
    {
        _source = null;
        _output = null;
        _sample = _lens = null;
        _backdrop = _sampleBrush = null;
        _blur = null;
        _effect = null;
        _sampleFallback = null;
        _expanded = Rect.Empty;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == BackgroundProperty || e.Property == MaterialProperty || e.Property == GlassEnabledProperty
            || e.Property == UseLightweightMaterialProperty)
            UpdateCoordinateTracking();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => RefreshCoordinates();

    private void RefreshCoordinates()
    {
        if (_checking || !_coordinateTracking || !IsLoaded || !IsVisible) return;
        _checking = true;
        try
        {
            var source = GetBackdropSource(this);
            // Popup visual trees have a separate presentation source. Resolve the explicit
            // background registered by their owner window, without capturing window contents.
            if (source is null && Application.Current is { } app)
                source = app.Windows.Cast<Window>().Select(GetBackdropSource).FirstOrDefault(s => s is not null);
            if (source is null || !source.IsVisible || source.ActualWidth <= 0 || source.ActualHeight <= 0 || IsAncestorOf(source) || source.IsAncestorOf(this))
            {
                if (_source is not null) { _source = null; InvalidateVisual(); }
                return;
            }
            Rect rect;
            if (PresentationSource.FromVisual(this) == PresentationSource.FromVisual(source))
                rect = TransformToVisual(source).TransformBounds(new Rect(RenderSize));
            else
                rect = new Rect(source.PointFromScreen(PointToScreen(new Point())), source.PointFromScreen(PointToScreen(new Point(ActualWidth, ActualHeight))));
            var sourceSize = source.RenderSize;
            if (_source != source || !Near(_sourceRect, rect) || _sourceSize != sourceSize)
            {
                _source = source; _sourceRect = rect; _sourceSize = sourceSize;
                InvalidateVisual();
            }
        }
        catch (InvalidOperationException) { _source = null; }
        finally { _checking = false; }
    }

    private static bool Near(Rect a, Rect b) => Math.Abs(a.X - b.X) < .05 && Math.Abs(a.Y - b.Y) < .05 && Math.Abs(a.Width - b.Width) < .05 && Math.Abs(a.Height - b.Height) < .05;

    protected override void OnRender(DrawingContext dc)
    {
        if (RenderSize.Width <= 0 || RenderSize.Height <= 0) return;
        if (Background is null) { base.OnRender(dc); return; }
        var bounds = new Rect(RenderSize);
        if (UsesLightweightMaterial)
        {
            DrawLightweightMaterial(dc, bounds);
            return;
        }
        if (_clip is null || _clipSize != RenderSize || _clipCorners != CornerRadius)
        {
            _clip = RoundedGeometry(bounds, CornerRadius);
            _clipSize = RenderSize; _clipCorners = CornerRadius;
        }
        dc.PushClip(_clip);
        if (!GlassEnabled || SystemParameters.HighContrast || !RenderCapability.IsPixelShaderVersionSupported(3, 0) || _source is null)
        {
            dc.DrawRectangle(FallbackBrush, null, bounds);
            if (Background is not null) dc.DrawRectangle(Background, null, bounds);
            if(Material==LiquidGlassMaterial.BottomTabIndicator)
            {
                var gutter=24+BlurRadius;
                dc.DrawRectangle(TabOverlay,null,new Rect(-gutter,-gutter,bounds.Width+2*gutter,bounds.Height+2*gutter));
                dc.DrawRoundedRectangle(null,new Pen(SystemColors.HighlightBrush,2),new Rect(1,1,Math.Max(0,bounds.Width-2),Math.Max(0,bounds.Height-2)),CornerRadius.TopLeft,CornerRadius.TopLeft);
            }
        }
        else
        {
            // A gutter keeps displaced and blurred samples outside the shape available.
            var gutter = 24 + BlurRadius;
            var expanded = new Rect(-gutter, -gutter, bounds.Width + 2 * gutter, bounds.Height + 2 * gutter);
            var viewbox = _sourceRect;
            viewbox.Inflate(gutter * _sourceRect.Width / bounds.Width, gutter * _sourceRect.Height / bounds.Height);
            _backdrop ??= new VisualBrush
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.Fill, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top,
                AutoLayoutContent = false
            };
            RenderOptions.SetCachingHint(_backdrop, CachingHint.Cache);
            if (_backdrop.Visual != _source) _backdrop.Visual = _source;
            _backdrop.Viewbox = viewbox;
            _sample ??= new DrawingVisual();
            var upstream=Material is LiquidGlassMaterial.BottomTabs or LiquidGlassMaterial.BottomTabIndicator;
            var indicator=Material==LiquidGlassMaterial.BottomTabIndicator;
            var progress=Math.Clamp(PressProgress,0,1);
            // Inputs must not cut sharp wallpaper-shaped holes into a blurred card.
            var blur = upstream ? 8 : Material == LiquidGlassMaterial.Chrome ? Math.Min(8, BlurRadius) : BlurRadius;
            _blur ??= new BlurEffect { RenderingBias = RenderingBias.Performance };
            _blur.Radius = blur; _blur.KernelType = BlurKernelType;
            var sampleEffect = blur > 0 ? _blur : null;
            if (_sample.Effect != sampleEffect) _sample.Effect = sampleEffect;
            var resized = _output is null || _expanded != expanded;
            if (resized || !ReferenceEquals(_sampleFallback, FallbackBrush))
            {
                using var context = _sample.RenderOpen();
                context.DrawRectangle(FallbackBrush, null, expanded);
                context.DrawRectangle(_backdrop, null, expanded);
                _sampleFallback = FallbackBrush;
            }
            var strong = Material is LiquidGlassMaterial.Chrome or LiquidGlassMaterial.Action;
            var dark = FallbackBrush is SolidColorBrush fallback && fallback.Color.R+fallback.Color.G+fallback.Color.B<384;
            var tint = (Background as SolidColorBrush)?.Color ?? Colors.Transparent;
            var alpha = tint.A / 255d * (Background?.Opacity ?? 1);
            // Legacy brushes often have opaque fills. Treat these as a surface tint,
            // with a material-specific ceiling instead of hiding the sampled wallpaper.
            if (!upstream)
            {
                var maximumTint = Material switch
                {
                    LiquidGlassMaterial.Input => dark ? .50 : .48,
                    LiquidGlassMaterial.Floating => dark ? .52 : .48,
                    LiquidGlassMaterial.Chrome => dark ? .32 : .22,
                    LiquidGlassMaterial.Action => dark ? .34 : .26,
                    _ => dark ? .50 : .48
                };
                alpha = Math.Min(alpha, maximumTint);
            }
            if(upstream) { tint=dark?Color.FromRgb(18,18,18):Color.FromRgb(250,250,250);alpha=.4; }
            var effect = _effect ??= new LiquidGlassEffect();
            effect.SizeConstant = LiquidGlassEffect.Vector(expanded.Width, expanded.Height, gutter, 0);
            effect.Corners = LiquidGlassEffect.Vector(CornerRadius.TopLeft, CornerRadius.TopRight, CornerRadius.BottomRight, CornerRadius.BottomLeft);
            effect.Lens = upstream ? LiquidGlassEffect.Vector(indicator?10*progress:24,indicator?14*progress:24,0,indicator?1:0) : LiquidGlassEffect.Vector(strong ? 18 : 14, (strong ? 14 : 10)*RefractionStrength, 0, strong ? .025 : 0);
            effect.Light = LiquidGlassEffect.Vector(upstream?.7071:-.7071, upstream?.7071:-.7071, upstream ? indicator?.5*progress:.5 : strong ? .75 : .48, dark ? 1 : 0);
            effect.Tint = LiquidGlassEffect.Vector(tint.R / 255d * alpha, tint.G / 255d * alpha, tint.B / 255d * alpha, alpha);
            effect.Controls=LiquidGlassEffect.Vector(upstream?1.5:1,0,indicator?2:upstream?1:0,progress);
            effect.OverlayTint=LiquidGlassEffect.Vector(0,dark?145d/255:136d/255,1,1);
            effect.Interaction=LiquidGlassEffect.Vector(InteractionPosition.X+gutter,InteractionPosition.Y+gutter,Math.Min(bounds.Width,bounds.Height)*1.5,InteractionProgress);
            effect.Tabs=TabOverlay is VisualBrush or ImageBrush or BitmapCacheBrush ? TabOverlay : EmptyTabs;
            _lens ??= new DrawingVisual();
            if (_lens.Effect != effect) _lens.Effect = effect;
            _sampleBrush ??= new VisualBrush(_sample) { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.Absolute };
            _output ??= new VisualBrush(_lens) { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.Absolute };
            RenderOptions.SetCachingHint(_sampleBrush, CachingHint.Cache);
            RenderOptions.SetCachingHint(_output, CachingHint.Cache);
            if (resized)
            {
                _sampleBrush.Viewbox = expanded;
                using var context = _lens.RenderOpen();
                context.DrawRectangle(_sampleBrush, null, expanded);
                _output.Viewbox = expanded;
                _expanded = expanded;
            }
            dc.DrawRectangle(_output, null, expanded);
            if (Background is not null && Background is not SolidColorBrush) dc.DrawRectangle(Background, null, bounds);
        }
        dc.Pop();
        if (BorderBrush is not null && BorderThickness.Left > 0)
        {
            var inset = BorderThickness.Left / 2;
            var strokeBounds = new Rect(inset, inset, Math.Max(0, bounds.Width - 2 * inset), Math.Max(0, bounds.Height - 2 * inset));
            var strokeCorners=new CornerRadius(Math.Max(0,CornerRadius.TopLeft-inset),Math.Max(0,CornerRadius.TopRight-inset),Math.Max(0,CornerRadius.BottomRight-inset),Math.Max(0,CornerRadius.BottomLeft-inset));
            dc.DrawGeometry(null,new Pen(BorderBrush,BorderThickness.Left),RoundedGeometry(strokeBounds,strokeCorners));
        }
    }

    private void DrawLightweightMaterial(DrawingContext dc, Rect bounds)
    {
        var background = Background;
        if (background is SolidColorBrush solid && solid.Opacity >= .999)
        {
            // Native surfaces lose the shader's blurred backdrop. Light theme text is
            // dark and needs a stronger neutral tint; dark theme keeps more wallpaper.
            var dark = FallbackBrush is SolidColorBrush fallback && fallback.Color.R + fallback.Color.G + fallback.Color.B < 384;
            if (solid.Color.A == 255)
            {
                var maximumTint = Material switch
                {
                    LiquidGlassMaterial.Input => dark ? .50 : .78,
                    LiquidGlassMaterial.Chrome => dark ? .32 : .84,
                    LiquidGlassMaterial.Action => dark ? .34 : .70,
                    _ => dark ? .50 : .86
                };
                var tint = Color.FromArgb((byte)Math.Round(255 * maximumTint), solid.Color.R, solid.Color.G, solid.Color.B);
                var capped = new SolidColorBrush(tint);
                capped.Freeze();
                background = capped;
            }
            else if (solid.Color.A > 0)
            {
                // Keep a readable minimum tint while preserving wallpaper visibility.
                var minimumTint = Material switch
                {
                    LiquidGlassMaterial.Input => dark ? .56 : .78,
                    LiquidGlassMaterial.Chrome => dark ? .60 : .84,
                    LiquidGlassMaterial.Action => dark ? .40 : .70,
                    _ => dark ? .60 : .86
                };
                var currentTint = solid.Color.A / 255d;
                if (currentTint < minimumTint)
                {
                    var tint = Color.FromArgb((byte)Math.Round(255 * minimumTint), solid.Color.R, solid.Color.G, solid.Color.B);
                    var readable = new SolidColorBrush(tint);
                    readable.Freeze();
                    background = readable;
                }
            }
        }
        dc.DrawGeometry(background, null, RoundedGeometry(bounds, CornerRadius));
        if (BorderBrush is null || BorderThickness.Left <= 0) return;
        var inset = BorderThickness.Left / 2;
        var strokeBounds = new Rect(inset, inset, Math.Max(0, bounds.Width - 2 * inset), Math.Max(0, bounds.Height - 2 * inset));
        var strokeCorners = new CornerRadius(Math.Max(0, CornerRadius.TopLeft - inset), Math.Max(0, CornerRadius.TopRight - inset),
            Math.Max(0, CornerRadius.BottomRight - inset), Math.Max(0, CornerRadius.BottomLeft - inset));
        dc.DrawGeometry(null, new Pen(BorderBrush, BorderThickness.Left), RoundedGeometry(strokeBounds, strokeCorners));
    }

    private static Geometry RoundedGeometry(Rect rect, CornerRadius corners)
    {
        var maximum = Math.Min(rect.Width,rect.Height)/2;
        var tl=Math.Clamp(corners.TopLeft,0,maximum);var tr=Math.Clamp(corners.TopRight,0,maximum);
        var br=Math.Clamp(corners.BottomRight,0,maximum);var bl=Math.Clamp(corners.BottomLeft,0,maximum);
        var geometry=new StreamGeometry();
        using(var c=geometry.Open())
        {
            c.BeginFigure(new Point(rect.Left+tl,rect.Top),true,true);
            c.LineTo(new Point(rect.Right-tr,rect.Top),true,false);
            c.ArcTo(new Point(rect.Right,rect.Top+tr),new Size(tr,tr),0,false,SweepDirection.Clockwise,true,false);
            c.LineTo(new Point(rect.Right,rect.Bottom-br),true,false);
            c.ArcTo(new Point(rect.Right-br,rect.Bottom),new Size(br,br),0,false,SweepDirection.Clockwise,true,false);
            c.LineTo(new Point(rect.Left+bl,rect.Bottom),true,false);
            c.ArcTo(new Point(rect.Left,rect.Bottom-bl),new Size(bl,bl),0,false,SweepDirection.Clockwise,true,false);
            c.LineTo(new Point(rect.Left,rect.Top+tl),true,false);
            c.ArcTo(new Point(rect.Left+tl,rect.Top),new Size(tl,tl),0,false,SweepDirection.Clockwise,true,false);
        }
        geometry.Freeze();return geometry;
    }
}
