using System.Drawing;

namespace SnapMark.Editor;

public interface IAnnotation
{
    Rectangle Bounds { get; set; }
    Color Color { get; set; }
    int StrokeWidth { get; set; }
    int ZOrder { get; set; }
    bool IsSelected { get; set; }
    
    void Draw(Graphics graphics);
    bool HitTest(Point point);
    void Move(Point delta);
    void Resize(Size newSize);
}


