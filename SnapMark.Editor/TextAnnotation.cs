using System.Drawing;

namespace SnapMark.Editor;

public class TextAnnotation : AnnotationBase
{
    public string Text { get; set; } = string.Empty;
    public Font Font { get; set; } = new Font("Arial", 12);
    public StringAlignment HorizontalAlignment { get; set; } = StringAlignment.Center;
    public StringAlignment VerticalAlignment { get; set; } = StringAlignment.Center;

    public TextAnnotation(Point location, string text) : base(new Rectangle(location, new Size(100, 30)))
    {
        Text = text;
    }

    public override void Draw(Graphics graphics)
    {
        using var brush = new SolidBrush(Color);
        using var stringFormat = new StringFormat
        {
            Alignment = HorizontalAlignment,
            LineAlignment = VerticalAlignment
        };

        graphics.DrawString(Text, Font, brush, Bounds, stringFormat);

        if (IsSelected)
        {
            DrawSelectionHandles(graphics);
        }
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
        var size = graphics.MeasureString(Text, Font);
        Bounds = new Rectangle(Bounds.Location, new Size((int)size.Width + 10, (int)size.Height + 10));
    }
}


