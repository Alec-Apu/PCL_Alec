using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.UI.Effects;

namespace PCL.Core.Test.UI;

[TestClass]
public class LiquidTabMotionTest
{
    [TestMethod]
    public void PositionMatchesComposeCriticalSpringEquation()
    {
        var spring=new LiquidSpring(1,1000,.001);spring.To(3);spring.Advance(.1);
        var omega=Math.Sqrt(1000);var expected=3*(1-(1+omega*.1)*Math.Exp(-omega*.1));
        Assert.AreEqual(expected,spring.Value,1e-12);
        Assert.AreEqual(3*1000*.1*Math.Exp(-omega*.1),spring.Velocity,1e-10);
    }
    [TestMethod]
    [DataRow(.6)] [DataRow(.7)]
    public void PressScaleMatchesUpstreamUnderdampedSpring(double damping)
    {
        var spring=new LiquidSpring(damping,250,.001,1);var target=78d/56;spring.To(target);spring.Advance(.15);
        var omega=Math.Sqrt(250);var frequency=omega*Math.Sqrt(1-damping*damping);var initial=1-target;
        var expected=target+Math.Exp(-damping*omega*.15)*(initial*Math.Cos(frequency*.15)+damping*omega*initial/frequency*Math.Sin(frequency*.15));
        Assert.AreEqual(expected,spring.Value,1e-12);
    }
    [TestMethod]
    public void RetargetPreservesPositionAndVelocity()
    {
        var spring=new LiquidSpring(1,1000,.001);spring.To(4);spring.Advance(.05);
        var value=spring.Value;var velocity=spring.Velocity;spring.To(1);
        Assert.AreEqual(value,spring.Value);Assert.AreEqual(velocity,spring.Velocity);
        spring.Advance(.01);Assert.IsTrue(double.IsFinite(spring.Value));
    }
    [TestMethod]
    public void ClickPressesThenReleasesAfterPositionNearTarget()
    {
        var motion=new LiquidTabMotion();motion.Reset(0,5);motion.Select(4);motion.Advance(.02);
        Assert.AreEqual(1,motion.Press.Target);Assert.IsTrue(motion.ScaleX.Value>1);
        for(var i=0;i<600;i++)motion.Advance(1d/240);
        Assert.AreEqual(4,motion.Position.Value);Assert.AreEqual(0,motion.Press.Value);
        Assert.AreEqual(1,motion.ScaleX.Value);Assert.AreEqual(1,motion.ScaleY.Value);Assert.IsFalse(motion.Active);
    }
    [TestMethod]
    public void DragNormalizesByCellWidthAndSnapsWithoutFling()
    {
        var motion=new LiquidTabMotion();motion.Reset(1,5);motion.BeginDrag();motion.Drag(180,100);
        Assert.AreEqual(2.8,motion.Position.Target,1e-10);Assert.AreEqual(3,motion.EndDrag());
        Assert.AreEqual(3,motion.Position.Target);Assert.AreEqual(0,motion.PanelDrag.Target);
    }
    [TestMethod]
    public void DragClampsAndMirrorsForRightToLeft()
    {
        var motion=new LiquidTabMotion();motion.Reset(2,5);motion.BeginDrag();motion.Drag(100,100,true);
        Assert.AreEqual(1,motion.Position.Target);motion.Drag(9999,100,true);Assert.AreEqual(0,motion.Position.Target);
        motion.Drag(-9999,100,true);Assert.AreEqual(4,motion.Position.Target);
    }
    [TestMethod]
    public void VelocityDeformationUsesOriginalClampedFormula()
    {
        var motion=new LiquidTabMotion();motion.Reset(0,5);motion.BeginDrag();
        for(var i=0;i<20;i++){motion.Drag(6,100);motion.Advance(1d/120);}
        Assert.IsTrue(motion.DragVelocity.Value>0);
        Assert.AreEqual(motion.ScaleX.Value/(1-Math.Clamp(motion.DragVelocity.Value/10*.75,-.2,.2)),motion.ThumbScaleX,1e-12);
        Assert.AreEqual(motion.ScaleY.Value*(1-Math.Clamp(motion.DragVelocity.Value/10*.25,-.2,.2)),motion.ThumbScaleY,1e-12);
    }
    [TestMethod]
    public void SpringsRemainConsistentAcrossFrameRates()
    {
        var a=new LiquidSpring(.6,250,.001,1);var b=new LiquidSpring(.6,250,.001,1);
        a.To(78d/56);b.To(78d/56);
        for(var i=0;i<12;i++)a.Advance(1d/60);
        for(var i=0;i<48;i++)b.Advance(1d/240);
        Assert.AreEqual(a.Value,b.Value,1e-10);Assert.AreEqual(a.Velocity,b.Velocity,1e-10);
    }
    [TestMethod]
    public void PanelOffsetUsesBoundedEaseOutAndMirrors()
    {
        Assert.AreEqual(0,LiquidTabMotion.PanelOffset(0,500));Assert.AreEqual(4,LiquidTabMotion.PanelOffset(1000,500),1e-6);
        Assert.AreEqual(-LiquidTabMotion.PanelOffset(100,500),LiquidTabMotion.PanelOffset(-100,500),1e-10);
        Assert.IsTrue(LiquidTabMotion.PanelOffset(100,500)>.8);
    }
    [TestMethod]
    public void ResetCancelsAllAnimationIncludingDrag()
    {
        var motion=new LiquidTabMotion();motion.Reset(0,5);motion.BeginDrag();motion.Drag(80,100);motion.Advance(.04);motion.Reset(2,3);
        Assert.IsFalse(motion.Active);Assert.AreEqual(2,motion.Position.Value);Assert.AreEqual(1,motion.ThumbScaleX);
    }
    [TestMethod]
    public void ReleaseDecaysDragVelocityAndRestoresBothScales()
    {
        var motion=new LiquidTabMotion();motion.Reset(0,5);motion.BeginDrag();
        for(var i=0;i<20;i++){motion.Drag(6,100);motion.Advance(1d/120);}
        Assert.IsTrue(motion.DragVelocity.Value>0);motion.EndDrag();
        for(var i=0;i<600;i++)motion.Advance(1d/240);
        Assert.AreEqual(0,motion.DragVelocity.Value);Assert.AreEqual(1,motion.ThumbScaleX);Assert.AreEqual(1,motion.ThumbScaleY);Assert.IsFalse(motion.Active);
    }
    [TestMethod]
    public void ScaleFinishesAtComposeEnvelopeDuration()
    {
        var spring=new LiquidSpring(.6,250,.001,1);spring.To(78d/56);
        Assert.AreEqual(.653,spring.Duration,1e-12);
        spring.Advance(.652);Assert.IsFalse(spring.Settled);
        spring.Advance(.0011);Assert.IsTrue(spring.Settled);Assert.AreEqual(78d/56,spring.Value);
    }
}
