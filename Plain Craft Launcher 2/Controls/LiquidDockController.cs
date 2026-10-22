// Gesture and layer adapter for Kyant's LiquidBottomTabs (Apache-2.0).
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PCL.Core.UI.Effects;

namespace PCL;

/// <summary>WPF gesture/render adapter for the upstream LiquidBottomTabs motion model.</summary>
internal sealed class LiquidDockController
{
    private readonly FormMain _form;
    internal LiquidTabMotion Motion { get; } = new();
    internal bool IsRendering => _rendering;
    private readonly Stopwatch _clock=Stopwatch.StartNew();
    private double _lastTime;
    private bool _rendering, _initialized, _ending;
    private double _pointerX;
    private RadioButton[] _tabs=[];
    private readonly DrawingVisual _accentTabs=new();
    private readonly Dictionary<FrameworkElement, VisualBrush> _tabBrushes = new();
    private readonly VisualBrush _tabOverlay;
    private readonly TranslateTransform _accentOffset = new();
    private readonly List<ScaleTransform> _accentScales = new();
    private readonly List<(FrameworkElement Content, Size Size)> _accentContents = new();
    private double _accentWidth = -1;
    private FlowDirection _accentDirection;
    private readonly DropShadowEffect _shadow=new() { Color=Colors.Black,BlurRadius=24,ShadowDepth=4,Direction=270 };
    internal LiquidDockController(FormMain form)
    {
        _form=form;
        _tabOverlay=new VisualBrush(_accentTabs) { ViewboxUnits=BrushMappingMode.Absolute,Stretch=Stretch.Fill,AutoLayoutContent=false };
        form.DockHighlight.TabOverlay=_tabOverlay;
        form.PrimaryNavigation.PreviewMouseLeftButtonDown+=Down;
        form.PrimaryNavigation.PreviewMouseMove+=Move;
        form.PrimaryNavigation.PreviewMouseLeftButtonUp+=Up;
        form.PrimaryNavigation.LostMouseCapture+=LostCapture;
        form.PrimaryNavigation.Unloaded+=(_,_)=>Stop();
    }
    private bool Animate => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    internal void Refresh(bool animate)
    {
        var tabs=_form.FluentDock.Children.OfType<RadioButton>().Where(t=>t.IsVisible).ToArray();
        if(tabs.Length==0 || _form.FluentDock.ActualWidth<=0)return;
        var selected=Array.FindIndex(tabs,t=>t.IsChecked==true);if(selected<0)selected=0;
        var countChanged=!_tabs.SequenceEqual(tabs);
        _tabs=tabs;
        if(countChanged) { _tabBrushes.Clear(); _accentWidth=-1; }
        if(!_initialized || countChanged || !Animate)
        {
            Motion.Reset(selected,tabs.Length);_initialized=true;Apply();Stop();return;
        }
        if(animate && Math.Abs(Motion.Position.Target-selected)>.001)Motion.Select(selected);
        Apply();if(Motion.Active)Start();
    }
    private void Down(object sender,MouseButtonEventArgs e)
    {
        if(!Animate || !_initialized)return;
        if(!BeginDrag(e.GetPosition(_form.PrimaryNavigation)))return;
        e.Handled=true;
    }
    internal bool BeginDrag(Point point,bool capture=true)
    {
        if(!Animate || !_initialized)return false;
        var left=Canvas.GetLeft(_form.DockHighlight);
        var width=_form.DockHighlight.Width;var sx=_form.DockHighlightScale.ScaleX;var sy=_form.DockHighlightScale.ScaleY;
        var hit=new Rect(left+width*(1-sx)/2,32-28*sy,width*sx,56*sy);
        if(!hit.Contains(point))return false;
        _pointerX=point.X;
        Motion.BeginDrag();if(capture)_form.PrimaryNavigation.CaptureMouse();Start();return true;
    }
    private void Move(object sender,MouseEventArgs e)
    {
        if(!Motion.Dragging)return;
        DragTo(e.GetPosition(_form.PrimaryNavigation).X);e.Handled=true;
    }
    internal void DragTo(double x)
    {
        if(!Motion.Dragging)return;
        Motion.Drag(x-_pointerX,_form.FluentDock.ActualWidth/_tabs.Length,_form.FlowDirection==FlowDirection.RightToLeft);
        _pointerX=x;Start();
    }
    private void Up(object sender,MouseButtonEventArgs e)
    {
        if(!Motion.Dragging)return;
        FinishDrag();e.Handled=true;
    }
    private void LostCapture(object sender,MouseEventArgs e) { if(Motion.Dragging && !_ending)FinishDrag(); }
    internal void FinishDrag()
    {
        _ending=true;
        try
        {
            var index=Motion.EndDrag();_form.PrimaryNavigation.ReleaseMouseCapture();
            _form.PageChange((FormMain.PageType)int.Parse((string)_tabs[index].Tag));
            Start();
        }
        finally { _ending=false; }
    }
    private void Start()
    {
        if(_rendering)return;_lastTime=_clock.Elapsed.TotalSeconds;
        CompositionTarget.Rendering+=Frame;_rendering=true;
    }
    private void Stop()
    {
        if(!_rendering)return;CompositionTarget.Rendering-=Frame;_rendering=false;
    }
    private void Frame(object? sender,EventArgs e)
    {
        if(!_form.PrimaryNavigation.IsLoaded) { Stop();return; }
        if(!Animate) { Refresh(false);return; }
        var time=_clock.Elapsed.TotalSeconds;Motion.Advance(time-_lastTime);_lastTime=time;
        Apply();if(!Motion.Active)Stop();
    }
    private void Apply()
    {
        if(_tabs.Length==0)return;
        var width=_form.FluentDock.ActualWidth/_tabs.Length;
        var offset=LiquidTabMotion.PanelOffset(Motion.PanelDrag.Value,_form.PrimaryNavigation.ActualWidth);
        var index=_form.FlowDirection==FlowDirection.RightToLeft ? _tabs.Length-1-Motion.Position.Value : Motion.Position.Value;
        var x=4+index*width+offset;
        Canvas.SetLeft(_form.DockHighlight,x);Canvas.SetTop(_form.DockHighlight,4);
        _form.DockHighlight.Width=width;_form.DockHighlight.Height=56;
        _form.DockHighlightScale.ScaleX=Motion.ThumbScaleX;_form.DockHighlightScale.ScaleY=Motion.ThumbScaleY;
        var panelScale=1+16/Math.Max(1,_form.PrimaryNavigation.ActualWidth)*Motion.Press.Value;
        _form.DockPanelScale.ScaleX=_form.DockPanelScale.ScaleY=panelScale;
        _form.DockPanelOffset.X=offset;
        _form.DockPanel.PressProgress=Motion.Press.Value;
        _form.DockPanel.InteractionProgress=Motion.Interaction.Value;
        _form.DockPanel.InteractionPosition=new Point(x+width/2,32);
        _form.DockHighlight.PressProgress=Motion.Press.Value;
        _form.DockHighlight.InteractionProgress=Motion.Interaction.Value;
        _shadow.Opacity=.1*Math.Clamp(Motion.Press.Value,0,1);
        _form.DockHighlight.Effect=_shadow.Opacity>0?_shadow:null;
        // The upstream invisible tabs backdrop magnifies content 1 -> 1.2 and
        // tints it with the platform accent; only the moving lens reveals it.
        var scale=1+.2*Motion.Press.Value;
        var rebuild = _accentWidth != _form.PrimaryNavigation.ActualWidth || _accentDirection != _form.FlowDirection;
        var contentCount=0;
        foreach(var tab in _tabs)
        {
            if(tab.Content is not FrameworkElement content || content.ActualWidth<=0)continue;
            if(contentCount>=_accentContents.Count || _accentContents[contentCount] != (content,content.RenderSize))rebuild=true;
            contentCount++;
        }
        if(contentCount!=_accentContents.Count)rebuild=true;
        if(rebuild)
        {
            _accentWidth=_form.PrimaryNavigation.ActualWidth;_accentDirection=_form.FlowDirection;
            _accentContents.Clear();_accentScales.Clear();
            using var dc=_accentTabs.RenderOpen();
            dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,_accentWidth,64));
            dc.PushTransform(_accentOffset);
            for(var i=0;i<_tabs.Length;i++)
            {
                if(_tabs[i].Content is not FrameworkElement content || content.ActualWidth<=0)continue;
                var cell=_form.FlowDirection==FlowDirection.RightToLeft?_tabs.Length-1-i:i;
                var center=new Point(4+(cell+.5)*width,32);
                var transform=new ScaleTransform(1,1,center.X,center.Y);
                _accentScales.Add(transform);_accentContents.Add((content,content.RenderSize));
                if(!_tabBrushes.TryGetValue(content,out var brush))
                    _tabBrushes[content]=brush=new VisualBrush(content) { AutoLayoutContent=false };
                dc.PushTransform(transform);
                dc.DrawRectangle(brush,null,new Rect(center.X-content.ActualWidth/2,center.Y-content.ActualHeight/2,content.ActualWidth,content.ActualHeight));
                dc.Pop();
            }
            dc.Pop();
        }
        _accentOffset.X=offset;
        foreach(var transform in _accentScales)transform.ScaleX=transform.ScaleY=scale;
        var gutter=24+_form.DockHighlight.BlurRadius;
        var sx=Motion.ThumbScaleX;var sy=Motion.ThumbScaleY;
        _tabOverlay.Viewbox=new Rect(x+width/2-width*sx/2-gutter*sx,32-28*sy-gutter*sy,(width+2*gutter)*sx,(56+2*gutter)*sy);
    }
}
