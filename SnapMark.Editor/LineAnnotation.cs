using System.Drawing;

namespace SnapMark.Editor;

public class LineAnnotation : AnnotationBase
{
    public Point StartPoint { get; set; }
    public Point EndPoint { get; set; }

    public LineAnnotation(Point start, Point end) : base(new Rectangle(start, new Size(end.X - start.X, end.Y - start.Y)))
    {
        StartPoint = start;
        EndPoint = end;
        UpdateBounds();
    }

    private void UpdateBounds()
    {
        int minX = Math.Min(StartPoint.X, EndPoint.X);
        int minY = Math.Min(StartPoint.Y, EndPoint.Y);
        int maxX = Math.Max(StartPoint.X, EndPoint.X);
        int maxY = Math.Max(StartPoint.Y, EndPoint.Y);
        
        Bounds = new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }

    public override void Draw(Graphics graphics)
    {
        using var pen = new Pen(Color, StrokeWidth);
        graphics.DrawLine(pen, StartPoint, EndPoint);

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

        graphics.FillEllipse(brush, StartPoint.X - halfSize, StartPoint.Y - halfSize, handleSize, handleSize);
        graphics.FillEllipse(brush, EndPoint.X - halfSize, EndPoint.Y - halfSize, handleSize, handleSize);
    }

    public override void Move(Point delta)
    {
        StartPoint = new Point(StartPoint.X + delta.X, StartPoint.Y + delta.Y);
        EndPoint = new Point(EndPoint.X + delta.X, EndPoint.Y + delta.Y);
        UpdateBounds();
    }

    public override bool HitTest(Point point)
    {
        // Check if point is near the line
        float distance = DistanceToLineSegment(point, StartPoint, EndPoint);
        return distance <= StrokeWidth + 5;
    }

    private static float DistanceToLineSegment(Point p, Point a, Point b)
    {
        float A = p.X - a.X;
        float B = p.Y - a.Y;
        float C = b.X - a.X;
        float D = b.Y - a.Y;

        float dot = A * C + B * D;
        float lenSq = C * C + D * D;
        float param = lenSq != 0 ? dot / lenSq : -1;

        float xx, yy;

        if (param < 0)
        {
            xx = a.X;
            yy = a.Y;
        }
        else if (param > 1)
        {
            xx = b.X;
            yy = b.Y;
        }
        else
        {
            xx = a.X + param * C;
            yy = a.Y + param * D;
        }

        float dx = p.X - xx;
        float dy = p.Y - yy;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
}


