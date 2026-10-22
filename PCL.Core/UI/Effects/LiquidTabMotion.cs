// Copyright 2025 Kyant. Kotlin-to-C# adaptation under Apache-2.0.
// Original: catalog/utils/DampedDragAnimation.kt and components/LiquidBottomTabs.kt.
using System;
using System.Collections.Generic;

namespace PCL.Core.UI.Effects;

/// <summary>Analytical unit-mass spring. Parameters match Compose's FloatSpringSpec.</summary>
public sealed class LiquidSpring(double dampingRatio, double stiffness, double threshold, double initial = 0)
{
    public double Value { get; private set; } = initial;
    public double Velocity { get; private set; }
    public double Target { get; private set; } = initial;
    private double _initial=initial,_initialVelocity,_elapsed,_duration;
    public bool Settled => _elapsed>=_duration;
    public double Duration => _duration;
    public void To(double target)
    {
        if(Target==target)return;
        _initial=Value;_initialVelocity=Velocity;Target=target;_elapsed=0;
        _duration=DurationEstimate((Value-target)/threshold,Velocity/threshold);
    }
    public void Snap(double value) { Value=Target=_initial=value;Velocity=_initialVelocity=_elapsed=_duration=0; }
    public void Advance(double seconds)
    {
        if(seconds<=0) return;
        _elapsed+=seconds;
        if(Settled){Snap(Target);return;}
        // Compose FloatSpringSpec samples SpringSimulation at integer milliseconds.
        var time=Math.Floor(_elapsed*1000+1e-7)/1000;
        var omega=Math.Sqrt(stiffness);var x=_initial-Target;var v=_initialVelocity;
        if(dampingRatio==1)
        {
            var b=v+omega*x;var decay=Math.Exp(-omega*time);
            Value=Target+(x+b*time)*decay;
            Velocity=(v-omega*b*time)*decay;
        }
        else
        {
            var decayRate=dampingRatio*omega;var frequency=omega*Math.Sqrt(1-dampingRatio*dampingRatio);
            var b=(v+decayRate*x)/frequency;var sine=Math.Sin(frequency*time);var cosine=Math.Cos(frequency*time);
            var decay=Math.Exp(-decayRate*time);var position=x*cosine+b*sine;
            Value=Target+position*decay;
            Velocity=decay*((-x*frequency*sine+b*frequency*cosine)-decayRate*position);
        }
    }
    // AndroidX SpringEstimation.kt (Copyright The Android Open Source Project,
    // Apache-2.0): envelope estimate for underdamping and Newton solve for critical.
    private double DurationEstimate(double displacement,double velocity)
    {
        if(displacement==0 && velocity==0)return 0;
        var p=Math.Abs(displacement);var v=displacement<0?-velocity:velocity;
        var r=-dampingRatio*Math.Sqrt(stiffness);double duration;
        if(dampingRatio<1)
        {
            var imaginary=Math.Sqrt(stiffness)*Math.Sqrt(1-dampingRatio*dampingRatio);
            var c2=(v-r*p)/imaginary;
            duration=Math.Log(1/Math.Sqrt(p*p+c2*c2))/r;
        }
        else
        {
            var c1=p;var c2=v-r*c1;
            var t1=Math.Log(Math.Abs(1/c1))/r;
            var guess=Math.Log(Math.Abs(1/c2));var t2=guess;
            for(var i=0;i<=5;i++)t2=guess-Math.Log(Math.Abs(t2/r));
            t2/=r;
            var current=!double.IsFinite(t1)?t2:!double.IsFinite(t2)?t1:Math.Max(t1,t2);
            var inflection=-(r*c1+c2)/(r*c2);
            var atInflection=(c1+c2*inflection)*Math.Exp(r*inflection);
            var signedDelta=-1d;
            if(!double.IsNaN(inflection) && inflection>0)
            {
                if(-atInflection<1){if(c2<0 && c1>0)current=0;}
                else { current=-2/r-c1/c2;signedDelta=1; }
            }
            var delta=double.MaxValue;
            for(var i=0;i<100 && delta>.001;i++)
            {
                var last=current;
                current-=((c1+c2*current)*Math.Exp(r*current)+signedDelta)/((c2*(r*current+1)+c1*r)*Math.Exp(r*current));
                delta=Math.Abs(last-current);
            }
            duration=current;
        }
        return double.IsFinite(duration)?Math.Max(0,Math.Truncate(duration*1000)/1000):0;
    }
}

/// <summary>
/// Port of Kyant's DampedDragAnimation/LiquidBottomTabs, pinned to
/// 65ab177e90e5c1d8c62e70cf7755841982da65f6. See shader NOTICE for license.
/// Time is supplied by the compositor or deterministic tests; no permanent timer.
/// </summary>
public sealed class LiquidTabMotion
{
    public LiquidSpring Position { get; } = new(1,1000,.001);
    public LiquidSpring DragVelocity { get; } = new(.5,300,.01);
    public LiquidSpring Press { get; } = new(1,1000,.001);
    public LiquidSpring ScaleX { get; } = new(.6,250,.001,1);
    public LiquidSpring ScaleY { get; } = new(.7,250,.001,1);
    public LiquidSpring PanelDrag { get; } = new(1,300,.5);
    public LiquidSpring Interaction { get; } = new(.5,300,.001);
    private readonly Queue<(double Time,double Value)> _positions = new();
    private double _time;
    private double _lastTrackedTime;
    private bool _releasePending;
    private bool _releaseFrame;
    public int Count { get; private set; } = 1;
    public bool Dragging { get; private set; }
    public bool Active => Dragging || _releasePending || !Position.Settled || !DragVelocity.Settled || !Press.Settled || !ScaleX.Settled || !ScaleY.Settled || !PanelDrag.Settled || !Interaction.Settled;
    public double ThumbScaleX => ScaleX.Value/(1-Math.Clamp(DragVelocity.Value/10*.75,-.2,.2));
    public double ThumbScaleY => ScaleY.Value*(1-Math.Clamp(DragVelocity.Value/10*.25,-.2,.2));
    public void Reset(int index,int count)
    {
        Count=Math.Max(1,count);Dragging=false;_releasePending=false;_positions.Clear();
        Position.Snap(Math.Clamp(index,0,Count-1));DragVelocity.Snap(0);Press.Snap(0);
        ScaleX.Snap(1);ScaleY.Snap(1);PanelDrag.Snap(0);Interaction.Snap(0);
    }
    private void PressDown()
    {
        _releasePending=false;_positions.Clear();Press.To(1);
        ScaleX.To(78d/56);ScaleY.To(78d/56);
    }
    public void Select(int index)
    {
        PressDown();Position.To(Math.Clamp(index,0,Count-1));DragVelocity.To(0);
        ReleaseWhenNearTarget();
    }
    public void BeginDrag()
    {
        Dragging=true;PressDown();Interaction.To(1);
    }
    public void Drag(double pixelDelta,double tabWidth,bool rightToLeft=false)
    {
        if(!Dragging || tabWidth<=0)return;
        Position.To(Math.Clamp(Position.Target+pixelDelta/tabWidth*(rightToLeft?-1:1),0,Count-1));
        PanelDrag.Snap(PanelDrag.Value+pixelDelta);
    }
    public int EndDrag()
    {
        var index=Math.Clamp((int)Math.Floor(Position.Target+.5),0,Count-1);
        Dragging=false;PressDown();Position.To(index);DragVelocity.To(0);PanelDrag.To(0);Interaction.To(0);ReleaseWhenNearTarget();return index;
    }
    private void ReleaseWhenNearTarget() { _releasePending=true;_releaseFrame=true; }
    public void Advance(double seconds)
    {
        _time+=seconds;Position.Advance(seconds);DragVelocity.Advance(seconds);
        if(Dragging)
        {
            if(_positions.Count>0 && _time-_lastTrackedTime>.04)_positions.Clear();
            _positions.Enqueue((_time,Position.Value));
            _lastTrackedTime=_time;
            while(_positions.Count>20 || (_positions.Count>1 && _time-_positions.Peek().Time>.1))_positions.Dequeue();
            DragVelocity.To(EstimateVelocity()/Math.Max(1,Count-1));
        }
        Press.Advance(seconds);ScaleX.Advance(seconds);ScaleY.Advance(seconds);PanelDrag.Advance(seconds);Interaction.Advance(seconds);
        if(_releasePending)
        {
            if(_releaseFrame) _releaseFrame=false;
            else if(Math.Abs(Position.Value-Position.Target)<Math.Max(.001,(Count-1)*.025))
            {
                _releasePending=false;Press.To(0);ScaleX.To(1);ScaleY.To(1);
            }
        }
    }
    // Compose uses a quadratic least-squares velocity fit over the most recent
    // 100ms (up to 20 samples). Fit in time relative to the latest sample.
    private double EstimateVelocity()
    {
        if(_positions.Count<3)return 0;
        double n=0,t=0,t2=0,t3=0,t4=0,y=0,ty=0,t2y=0;
        foreach(var sample in _positions)
        {
            var x=sample.Time-_time;var xx=x*x;n++;t+=x;t2+=xx;t3+=xx*x;t4+=xx*xx;
            y+=sample.Value;ty+=x*sample.Value;t2y+=xx*sample.Value;
        }
        var determinant=n*(t2*t4-t3*t3)-t*(t*t4-t2*t3)+t2*(t*t3-t2*t2);
        if(Math.Abs(determinant)<1e-18)return 0;
        return (n*(ty*t4-t3*t2y)-y*(t*t4-t2*t3)+t2*(t*t2y-ty*t2))/determinant;
    }
    public static double PanelOffset(double drag,double width)
    {
        if(width<=0)return 0;
        var fraction=Math.Clamp(drag/width,-1,1);var x=Math.Abs(fraction);
        // Compose EaseOut = cubic-bezier(0,0,.58,1), solved for its x coordinate.
        var low=0d;var high=1d;
        for(var i=0;i<24;i++){var u=(low+high)/2;var curve=3*(1-u)*u*u*.58+u*u*u;if(curve<x)low=u;else high=u;}
        var s=(low+high)/2;return 4*Math.Sign(fraction)*(3*(1-s)*s*s+s*s*s);
    }
}
