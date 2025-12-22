using System.Drawing;

namespace SnapMark.Editor;

public class RectangleAnnotation : AnnotationBase
{
    public bool IsFilled { get; set; } = false;
    public int CornerRadius { get; set; } = 0; // 0 = sharp corners

    public RectangleAnnotation(Rectangle bounds) : base(bounds)
    {
    }

    public override void Draw(Graphics graphics)
    {
        using var pen = new Pen(Color, StrokeWidth);
        
        if (CornerRadius > 0)
        {
            // Rounded rectangle
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddRoundedRectangle(Bounds, CornerRadius);
            
            if (IsFilled)
            {
                using var brush = new SolidBrush(Color.FromArgb(128, Color));
                graphics.FillPath(brush, path);
            }
            graphics.DrawPath(pen, path);
        }
        else
        {
            // Regular rectangle
            if (IsFilled)
            {
                using var brush = new SolidBrush(Color.FromArgb(128, Color));
                graphics.FillRectangle(brush, Bounds);
            }
            graphics.DrawRectangle(pen, Bounds);
        }

        if (IsSelected)
        {
            DrawSelectionHandles(graphics);
        }
    }

    private void DrawSelectionHandles(Graphics graphics)
    {
        using var brush = new SolidBrush(Color.Blue);
        var handleSize = 8;
        var halfSize = handleSize / 2;

        // Draw 8 handles (corners and midpoints)
        var handles = new[]
        {
            new Point(Bounds.Left, Bounds.Top),
            new Point(Bounds.Right, Bounds.Top),
            new Point(Bounds.Right, Bounds.Bottom),
            new Point(Bounds.Left, Bounds.Bottom),
            new Point(Bounds.Left + Bounds.Width / 2, Bounds.Top),
            new Point(Bounds.Right, Bounds.Top + Bounds.Height / 2),
            new Point(Bounds.Left + Bounds.Width / 2, Bounds.Bottom),
            new Point(Bounds.Left, Bounds.Top + Bounds.Height / 2)
        };

        foreach (var handle in handles)
        {
            graphics.FillEllipse(brush, handle.X - halfSize, handle.Y - halfSize, handleSize, handleSize);
        }
    }
}


