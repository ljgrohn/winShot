using System.Drawing;
using System.Drawing.Drawing2D;

namespace SnapMark.Editor;

public class TextAnnotation : AnnotationBase
{
    public string Text { get; set; } = string.Empty;
    public Font Font { get; set; } = new Font("Arial", 12);
    public StringAlignment HorizontalAlignment { get; set; } = StringAlignment.Center;
    public StringAlignment VerticalAlignment { get; set; } = StringAlignment.Center;
    
    // Custom vertical padding (top and bottom) - can be adjusted by resizing
    // If null, uses default 3% of text height
    public int? CustomTopPadding { get; set; }
    public int? CustomBottomPadding { get; set; }

    public TextAnnotation(Point location, string text) : base(new Rectangle(location, new Size(100, 30)))
    {
        Text = text;
    }

    public override void Draw(Graphics graphics)
    {
        // Calculate text size to determine padding - handle multi-line text
        var stringFormat = new StringFormat(StringFormatFlags.LineLimit)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        var textSize = graphics.MeasureString(Text, Font, int.MaxValue, stringFormat);
        var textWidth = textSize.Width;
        var textHeight = textSize.Height;
        
        // Calculate padding: 2-character buffer on left/right, custom or default top/bottom
        var twoCharSize = graphics.MeasureString("MM", Font);
        var horizontalPadding = (int)twoCharSize.Width;
        var topPadding = CustomTopPadding ?? (int)(textHeight * 0.03);
        var bottomPadding = CustomBottomPadding ?? (int)(textHeight * 0.03);
        
        // Calculate text rectangle within bounds with padding
        var textRect = new Rectangle(
            Bounds.X + horizontalPadding,
            Bounds.Y + topPadding,
            Bounds.Width - (horizontalPadding * 2),
            Bounds.Height - topPadding - bottomPadding);
        
        // Draw rounded rectangle background
        var backgroundColor = isBlack ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(0x1a, 0x1a, 0x1a);
        using var backgroundBrush = new SolidBrush(backgroundColor);
        var cornerRadius = 4;
        DrawRoundedRectangle(graphics, backgroundBrush, Bounds, cornerRadius);
        
        // Draw text centered (supports multi-line)
        using var brush = new SolidBrush(Color);
        graphics.DrawString(Text, Font, brush, textRect, stringFormat);
        stringFormat.Dispose();

        if (IsSelected)
        {
            DrawSelectionHandles(graphics);
        }
    }
    
    private bool isBlack => Color.R == 0 && Color.G == 0 && Color.B == 0;
    
    private void DrawRoundedRectangle(Graphics graphics, Brush brush, Rectangle rect, int radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90); // Top-left
        path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90); // Top-right
        path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90); // Bottom-right
        path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90); // Bottom-left
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }

    private void DrawSelectionHandles(Graphics graphics)
    {
        using var pen = new Pen(Color.Blue, 2);
        graphics.DrawRectangle(pen, Bounds);

        using var brush = new SolidBrush(Color.Blue);
        var handleSize = 8;
        var halfSize = handleSize / 2;

        // Draw resize handles at corners
        graphics.FillEllipse(brush, Bounds.Left - halfSize, Bounds.Top - halfSize, handleSize, handleSize);
        graphics.FillEllipse(brush, Bounds.Right - halfSize, Bounds.Top - halfSize, handleSize, handleSize);
        graphics.FillEllipse(brush, Bounds.Right - halfSize, Bounds.Bottom - halfSize, handleSize, handleSize);
        graphics.FillEllipse(brush, Bounds.Left - halfSize, Bounds.Bottom - halfSize, handleSize, handleSize);
    }

    public void UpdateBoundsFromText(Graphics graphics)
    {
        // Measure text size - handle multi-line text
        var stringFormat = new StringFormat(StringFormatFlags.LineLimit);
        var size = graphics.MeasureString(Text, Font, int.MaxValue, stringFormat);
        stringFormat.Dispose();
        
        // Calculate padding: 2-character buffer on left/right, custom or default top/bottom
        // Measure 2 characters (using "MM" as a reasonable width estimate)
        var twoCharSize = graphics.MeasureString("MM", Font);
        var horizontalPadding = (int)twoCharSize.Width;
        var topPadding = CustomTopPadding ?? (int)(size.Height * 0.03);
        var bottomPadding = CustomBottomPadding ?? (int)(size.Height * 0.03);
        
        // Only update bounds if custom padding hasn't been set (auto-size mode)
        // If custom padding is set, preserve the current bounds height
        if (!CustomTopPadding.HasValue && !CustomBottomPadding.HasValue)
        {
            Bounds = new Rectangle(
                Bounds.Location, 
                new Size(
                    (int)size.Width + (horizontalPadding * 2), 
                    (int)size.Height + topPadding + bottomPadding));
        }
        else
        {
            // Update width based on text, but preserve height (which is controlled by custom padding)
            Bounds = new Rectangle(
                Bounds.Location, 
                new Size(
                    (int)size.Width + (horizontalPadding * 2), 
                    Bounds.Height));
        }
    }
}


