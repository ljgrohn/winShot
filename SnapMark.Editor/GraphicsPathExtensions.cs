using System.Drawing;
using System.Drawing.Drawing2D;

namespace SnapMark.Editor;

public static class GraphicsPathExtensions
{
    public static void AddRoundedRectangle(this GraphicsPath path, Rectangle rect, int radius)
    {
        int diameter = radius * 2;
        Rectangle arcRect = new Rectangle(rect.Location, new Size(diameter, diameter));

        // Top left arc
        path.AddArc(arcRect, 180, 90);

        // Top right arc
        arcRect.X = rect.Right - diameter;
        path.AddArc(arcRect, 270, 90);

        // Bottom right arc
        arcRect.Y = rect.Bottom - diameter;
        path.AddArc(arcRect, 0, 90);

        // Bottom left arc
        arcRect.X = rect.Left;
        path.AddArc(arcRect, 90, 90);

        path.CloseFigure();
    }
}


