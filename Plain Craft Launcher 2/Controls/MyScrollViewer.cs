using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.UI.Controls;

namespace PCL;

public class MyScrollViewer : ScrollViewer
{
    public static readonly DependencyProperty FixedHeaderProperty = DependencyProperty.Register(
        nameof(FixedHeader), typeof(object), typeof(MyScrollViewer),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, (sender, e) =>
        {
            var scroll=(MyScrollViewer)sender;
            if(e.OldValue is not null)scroll.RemoveLogicalChild(e.OldValue);
            if(e.NewValue is not null)scroll.AddLogicalChild(e.NewValue);
        }));

    public object? FixedHeader
    {
        get => GetValue(FixedHeaderProperty);
        set => SetValue(FixedHeaderProperty, value);
    }

    protected override System.Collections.IEnumerator LogicalChildren => EnumerateLogicalChildren().GetEnumerator();

    private IEnumerable<object> EnumerateLogicalChildren()
    {
        var children=base.LogicalChildren;
        while(children is not null && children.MoveNext())yield return children.Current;
        if(FixedHeader is { } header)yield return header;
    }

    private double realOffset;
    private double _targetOffset;
    private long _lastWheel;


    public MyScrollBar scrollBar;

    public MyScrollViewer()
    {
        ScrollChanged += MyScrollViewer_ScrollChanged;
        IsVisibleChanged += MyScrollViewer_IsVisibleChanged;
        Loaded += (_, _) => Load();
        PreviewGotKeyboardFocus += MyScrollViewer_PreviewGotKeyboardFocus;
    }

    public double DeltaMult { get; set; } = 1d;

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (e.Delta != 0 && VerticalScrollBarVisibility == ScrollBarVisibility.Disabled && ScrollableWidth > 0)
        {
            ScrollToHorizontalOffset(HorizontalOffset - e.Delta);
            e.Handled = true;
            return;
        }
        if (e.Handled || e.Delta == 0 || ScrollableHeight <= 0d ||
            (e.Delta > 0 && VerticalOffset <= 0) ||
            (e.Delta < 0 && VerticalOffset >= ScrollableHeight))
            return;

        var src = e.Source;
        if (Content is FrameworkElement element && element.TemplatedParent is null)
        {
            switch (src)
            {
                case ComboBox { IsDropDownOpen: true }:
                case TextBox { AcceptsReturn: true }:
                case ComboBoxItem:
                case CheckBox:
                    return;
            }
        }

        var lines = SystemParameters.WheelScrollLines;
        if (lines == 0) { e.Handled = true; return; }
        e.Handled = true;
        var distance = lines < 0 ? ViewportHeight : lines * 16d;
        PerformVerticalOffsetDelta(-e.Delta / 120d * distance);

        Tooltip.Dismiss();
    }

    public void PerformVerticalOffsetDelta(double delta)
    {
        var now = Environment.TickCount64;
        if (now - _lastWheel > 200) _targetOffset = VerticalOffset;
        _lastWheel = now;
        _targetOffset = ModBase.MathClamp(_targetOffset + delta * DeltaMult, 0d, ScrollableHeight);
        if (!SystemParameters.ClientAreaAnimation)
        {
            ScrollToVerticalOffset(_targetOffset);
            return;
        }
        ModAnimation.AniStart(ModAnimation.AaDouble(animDelta =>
        {
            realOffset = ModBase.MathClamp(realOffset + (double)animDelta, 0d, ScrollableHeight);
            ScrollToVerticalOffset(realOffset);
        }, _targetOffset - VerticalOffset, 140, 0, new ModAnimation.AniEaseOutFluent((ModAnimation.AniEasePower)6), false), "Scroll " + GetHashCode());
    }

    private void MyScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        realOffset = VerticalOffset;
        if (ModMain.frmMain is not null &&
            (e.VerticalChange != 0 || e.ViewportHeightChange != 0))
            ModMain.frmMain.BtnExtraBack.ShowRefresh();
    }

    private void MyScrollViewer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        ModMain.frmMain.BtnExtraBack.ShowRefresh();
    }

    private void Load()
    {
        scrollBar = (MyScrollBar)GetTemplateChild("PART_VerticalScrollBar");
    }

    private void MyScrollViewer_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is MySlider)
            e.Handled = true; // #3854，阻止获得焦点时自动滚动
    }
}