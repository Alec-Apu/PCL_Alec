using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace PCL;

[ContentProperty("Inlines")]
public partial class MyButton
{
    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e); // 自定义事件

    public enum ColorState
    {
        Normal = 0,
        Highlight = 1,
        Red = 2
    }

    // 自定义事件
    private const int animationColorIn = 100;
    private const int animationColorOut = 200;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyButton)sender).LabText.Text = (string)e.NewValue;
        }));

    // 属性穿透
    public new static readonly DependencyProperty PaddingProperty = DependencyProperty.Register("Padding",
        typeof(Thickness), typeof(MyButton), new PropertyMetadata(new Thickness(12, 0, 12, 0), (sender, e) =>
        {
            if (sender is MyButton { LabText: not null } button) button.LabText.Padding = (Thickness)e.NewValue;
        }));
    
    private ColorState _ColorType = ColorState.Normal; // 配色方案

    // 鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    

    // 自定义属性
    public int Uuid = ModBase.GetUuid();

    static MyButton()
    {
        HorizontalAlignmentProperty.OverrideMetadata(typeof(MyButton),
            new FrameworkPropertyMetadata(HorizontalAlignment.Left));
    }

    public MyButton()
    {
        InitializeComponent();
        LabText.Padding = Padding;

        MouseEnter += RefreshColor;
        MouseLeave += RefreshColor;
        Loaded += RefreshColor;
        IsEnabledChanged += (_, _) => RefreshColor();
        IsKeyboardFocusWithinChanged += (_, _) =>
        {
            if (_ColorType != ColorState.Normal || HoverMask is null || !IsEnabled) return;
            if (IsKeyboardFocusWithin && !IsMouseOver)
                AnimateHoverMask(0.5d, animationColorIn);
            else if (!IsMouseOver)
                AnimateHoverMask(0d, animationColorOut);
        };
        KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Space) || !IsEnabled || e.IsRepeat) return;
            e.Handled = true;
            Click?.Invoke(this, new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left));
            ModMain.RaiseCustomEvent(this);
        };
        MouseLeftButtonUp += Button_MouseUp;
        MouseLeftButtonDown += Button_MouseDown;
        MouseEnter += (_, _) => Button_MouseEnter();
        MouseLeftButtonUp += (_, _) => Button_MouseUp();
        MouseLeave += (_, _) => Button_MouseLeave();
    }

    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 显示文本

    public Thickness TextPadding
    {
        get => LabText.Padding;
        set => LabText.Padding = value;
    }

    public ColorState ColorType
    {
        get => _ColorType;
        set
        {
            _ColorType = value;
            RefreshColor();
        }
    }

    public new Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public Transform RealRenderTransform
    {
        get => PanFore.RenderTransform;
        set => PanFore.RenderTransform = value;
    }

    // 声明
    public event ClickEventHandler? Click;

    private string GetBorderBrushResourceKey()
    {
        return ColorType switch
        {
            ColorState.Normal => "Fluent.Stroke",
            ColorState.Highlight => IsMouseOver ? "ColorBrush3" : "ColorBrush2",
            ColorState.Red => IsMouseOver ? "ColorBrushRedLight" : "ColorBrushRedDark",
            _ => "ColorBrush1"
        };
    }

    private void StartBorderBrushAnimation(string resourceKey, int duration)
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaColor(PanFore, BorderBrushProperty, resourceKey, duration)
            }, "MyButton Color " + Uuid);
    }

    private void RefreshColor(object obj = null, object e = null)
    {
        if (!IsEnabled && HoverMask is not null)
        {
            ModAnimation.AniStop("MyButton Hover " + Uuid);
            HoverMask.Opacity = 0d;
        }
        LabText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
            !IsEnabled ? "ColorBrushGray3" : ColorType == ColorState.Highlight ? "Fluent.OnAccent" : "ColorBrush1");
        if (ColorType == ColorState.Highlight)
            PanFore.SetResourceReference(BackgroundProperty, IsEnabled ? "Fluent.AccentFill" : "Fluent.GlassSurface");
        // PanFore has no painted border by default. Animating its BorderBrush only
        // creates invisible brush work on load/hover; keep visible-border callers unchanged.
        if (PanFore.BorderThickness == new Thickness(0))
        {
            ModAnimation.AniStop("MyButton Color " + Uuid);
            PanFore.SetResourceReference(BorderBrushProperty, IsEnabled ? GetBorderBrushResourceKey() : "ColorBrushGray4");
            return;
        }
        try
        {
            if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
            {
                if (IsEnabled)
                    StartBorderBrushAnimation(GetBorderBrushResourceKey(), IsMouseOver ? animationColorIn : animationColorOut);
                else
                    // 不可用（Gray 4）
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaColor(PanFore, BorderBrushProperty,
                                ThemeManager.colorGray4 - PanFore.BorderBrush, animationColorOut)
                        }, "MyButton Color " + Uuid);
            }
            else
            {
                ModAnimation.AniStop("MyButton Color " + Uuid);
                if (IsEnabled)
                    PanFore.SetResourceReference(BorderBrushProperty, GetBorderBrushResourceKey());
                else
                    PanFore.BorderBrush = ThemeManager.colorGray4;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新按钮颜色出错");
        }
    }

    // 实现自定义事件
    private bool isMouseDown = false;
    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!isMouseDown)
            return;
        ModBase.Log("[Control] 按下按钮：" + Text);
        Click?.Invoke(sender, e);
        ModMain.RaiseCustomEvent(this);
    }

    private void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        isMouseDown = true;
        Focus();
        if (_ColorType == ColorState.Normal)
            SetHoverMask(true);
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(PanFore, 0.97d - ((ScaleTransform)PanFore.RenderTransform).ScaleX, 90,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
            "MyButton Scale " + Uuid);
    }

    private void Button_MouseEnter()
    {
        if (_ColorType == ColorState.Normal)
        {
            SetHoverMask(false);
            AnimateHoverMask(1d, animationColorIn);
            return;
        }
        ModAnimation.AniStart(
            ModAnimation.AaColor(PanFore, BackgroundProperty,
                _ColorType == ColorState.Red ? "ColorBrushRedBack" : "Fluent.AccentHover", animationColorIn),
            "MyButton Background " + Uuid);
    }

    private void Button_MouseUp()
    {
        if (!isMouseDown)
            return;
        isMouseDown = false;
        if (_ColorType == ColorState.Normal)
            SetHoverMask(false);
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(PanFore, 1d - ((ScaleTransform)PanFore.RenderTransform).ScaleX, 220,
                ease: new ModAnimation.AniEaseOutFluent()), "MyButton Scale " + Uuid);
    }

    private void Button_MouseLeave()
    {
        if (_ColorType == ColorState.Normal)
        {
            AnimateHoverMask(IsKeyboardFocusWithin ? 0.5d : 0d, animationColorOut);
        }
        else
        {
            ModAnimation.AniStart(
                ModAnimation.AaColor(PanFore, BackgroundProperty,
                    _ColorType == ColorState.Highlight ? IsEnabled ? "Fluent.AccentFill" : "Fluent.GlassSurface" : "Fluent.GlassSurface",
                    animationColorOut),
                "MyButton Background " + Uuid);
        }
        if (!isMouseDown)
            return;
        isMouseDown = false;
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(PanFore, 1d - ((ScaleTransform)PanFore.RenderTransform).ScaleX, 240,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)), "MyButton Scale " + Uuid);
    }

    private void SetHoverMask(bool pressed)
    {
        HoverMask?.SetResourceReference(Border.BackgroundProperty,
            pressed ? "Fluent.ActionPressed" : "Fluent.ActionHover");
    }

    private void AnimateHoverMask(double value, int time)
    {
        if (HoverMask is null)
            return;
        if (!ControlVisualHelpers.ShouldAnimate(this))
        {
            ModAnimation.AniStop("MyButton Hover " + Uuid);
            HoverMask.Opacity = value;
            return;
        }
        ModAnimation.AniStart(
            ModAnimation.AaOpacity(HoverMask, value - HoverMask.Opacity, time,
                ease: new ModAnimation.AniEaseOutFluent()), "MyButton Hover " + Uuid);
    }
}
