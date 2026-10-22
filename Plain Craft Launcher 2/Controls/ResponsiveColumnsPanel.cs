using System.Windows;
using System.Windows.Controls;

namespace PCL;

/// <summary>Two-column groups become vertical without fixed row heights or clipped labels.</summary>
public sealed class ResponsiveColumnsPanel : Panel
{
    public static readonly DependencyProperty MinimumColumnWidthProperty = DependencyProperty.Register(nameof(MinimumColumnWidth), typeof(double), typeof(ResponsiveColumnsPanel), new FrameworkPropertyMetadata(280d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty SecondaryWidthProperty = DependencyProperty.Register(nameof(SecondaryWidth), typeof(double), typeof(ResponsiveColumnsPanel), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(nameof(Gap), typeof(double), typeof(ResponsiveColumnsPanel), new FrameworkPropertyMetadata(16d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double MinimumColumnWidth { get => (double)GetValue(MinimumColumnWidthProperty); set => SetValue(MinimumColumnWidthProperty, value); }
    public double SecondaryWidth { get => (double)GetValue(SecondaryWidthProperty); set => SetValue(SecondaryWidthProperty, value); }
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }
    private bool Stack(double width) => width < MinimumColumnWidth + (double.IsNaN(SecondaryWidth) ? MinimumColumnWidth : SecondaryWidth) + Gap;
    protected override Size MeasureOverride(Size available)
    {
        var children = InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToArray();
        var width = double.IsFinite(available.Width) ? available.Width : 800;
        var stacked = Stack(width) || children.Length != 2;
        var second = double.IsNaN(SecondaryWidth) ? Math.Max(0,(width-Gap)/2) : Math.Min(width,SecondaryWidth);
        var height = 0d;
        for (var i=0;i<children.Length;i++)
        {
            children[i].Measure(new Size(stacked ? width : i == 0 ? Math.Max(0,width-Gap-second) : second,double.PositiveInfinity));
            height=stacked ? height+children[i].DesiredSize.Height+(i>0?Gap:0) : Math.Max(height,children[i].DesiredSize.Height);
        }
        return new Size(width,height);
    }
    protected override Size ArrangeOverride(Size final)
    {
        var children=InternalChildren.Cast<UIElement>().Where(c=>c.Visibility!=Visibility.Collapsed).ToArray();
        var stacked=Stack(final.Width)||children.Length!=2;
        var second=double.IsNaN(SecondaryWidth)?Math.Max(0,(final.Width-Gap)/2):Math.Min(final.Width,SecondaryWidth);
        var y=0d;
        for(var i=0;i<children.Length;i++)
        {
            if(stacked) { children[i].Arrange(new Rect(0,y,final.Width,children[i].DesiredSize.Height)); y+=children[i].DesiredSize.Height+Gap; }
            else children[i].Arrange(new Rect(i==0?0:final.Width-second,0,i==0?Math.Max(0,final.Width-Gap-second):second,final.Height));
        }
        return final;
    }
}
