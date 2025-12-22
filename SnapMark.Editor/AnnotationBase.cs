using System.Drawing;

namespace SnapMark.Editor;

public abstract class AnnotationBase : IAnnotation
{
    public Rectangle Bounds { get; set; }
    public Color Color { get; set; } = Color.Red;
    public int StrokeWidth { get; set; } = 2;
    public int ZOrder { get; set; }
    public bool IsSelected { get; set; }

    protected AnnotationBase(Rectangle bounds)
    {
        Bounds = bounds;
    }

    public abstract void Draw(Graphics graphics);

    public virtual bool HitTest(Point point)
    {
        return Bounds.Contains(point);
    }

    public virtual void Move(Point delta)
    {
        Bounds = new Rectangle(
            Bounds.X + delta.X,
            Bounds.Y + delta.Y,
            Bounds.Width,
            Bounds.Height);
    }

    public virtual void Resize(Size newSize)
    {
        Bounds = new Rectangle(Bounds.Location, newSize);
    }
}


