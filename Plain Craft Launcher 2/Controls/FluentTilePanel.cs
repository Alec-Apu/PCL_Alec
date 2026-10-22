using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using PCL.Core.UI.Controls;

namespace PCL;

/// <summary>
/// Responsive instance tiles. Deriving from StackPanel preserves the existing lazy card
/// population and instance removal contracts while replacing its layout algorithm.
/// </summary>
public class FluentTilePanel : StackPanel
{
    public static readonly DependencyProperty TileBackgroundProperty = DependencyProperty.Register(
        "TileBackground", typeof(Brush), typeof(FluentTilePanel), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TileBorderProperty = DependencyProperty.Register(
        "TileBorder", typeof(Brush), typeof(FluentTilePanel), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public FluentTilePanel()
    {
        SetResourceReference(TileBackgroundProperty, "Fluent.Card");
        SetResourceReference(TileBorderProperty, "Fluent.Stroke");
    }

    private int _columns = 1;
    private const double Gap = 12;
    private readonly List<double> _rowHeights = [];
    private readonly Dictionary<MyListItem,LiquidGlassSurface> _tileSurfaces = [];

    protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        if (visualRemoved is MyListItem removed && _tileSurfaces.Remove(removed,out var oldSurface)) removed.Children.Remove(oldSurface);
        if (visualAdded is not MyListItem item) return;
        var surface=new LiquidGlassSurface { CornerRadius=new CornerRadius(16), BorderThickness=new Thickness(1), IsHitTestVisible=false };
        surface.SetBinding(Border.BackgroundProperty,new Binding(nameof(TileBackground)) { Source=this });
        surface.SetBinding(Border.BorderBrushProperty,new Binding(nameof(TileBorder)) { Source=this });
        Grid.SetColumnSpan(surface,999);Grid.SetRowSpan(surface,999);
        item.Children.Insert(0,surface);_tileSurfaces[item]=surface;
    }

    public Brush TileBackground { get => (Brush)GetValue(TileBackgroundProperty); set => SetValue(TileBackgroundProperty,value); }
    public Brush TileBorder { get => (Brush)GetValue(TileBorderProperty); set => SetValue(TileBorderProperty,value); }

    protected override Size MeasureOverride(Size constraint)
    {
        var width = double.IsInfinity(constraint.Width) ? 720 : Math.Max(0, constraint.Width);
        _columns = Math.Max(1, (int)((width + Gap) / 292));
        var cellWidth = Math.Max(0, (width - Gap * (_columns - 1)) / _columns);
        _rowHeights.Clear();
        var index = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (child is not MyListItem) { child.Measure(new Size(0, 0)); continue; }
            child.Measure(new Size(cellWidth, double.PositiveInfinity));
            var row = index++ / _columns;
            if (row == _rowHeights.Count) _rowHeights.Add(0);
            _rowHeights[row] = Math.Max(_rowHeights[row], child.DesiredSize.Height);
        }
        return new Size(width, _rowHeights.Sum() + Gap * Math.Max(0, _rowHeights.Count - 1) + 18);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var cellWidth = Math.Max(0, (arrangeSize.Width - Gap * (_columns - 1)) / _columns);
        var index = 0;
        var y = 0d;
        foreach (UIElement child in InternalChildren)
        {
            if (child is not MyListItem) { child.Arrange(new Rect(0, y, 0, 0)); continue; }
            var column = index % _columns;
            var row = index++ / _columns;
            if (column == 0 && row > 0) y += _rowHeights[row - 1] + Gap;
            child.Arrange(new Rect(column * (cellWidth + Gap), y, cellWidth, _rowHeights[row]));
        }
        InvalidateVisual();
        return arrangeSize;
    }

}
