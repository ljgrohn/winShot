using System.Drawing;

namespace SnapMark.Editor;

public class CreateAnnotationCommand : ICommand
{
    private readonly AnnotationCollection _collection;
    private readonly IAnnotation _annotation;

    public CreateAnnotationCommand(AnnotationCollection collection, IAnnotation annotation)
    {
        _collection = collection;
        _annotation = annotation;
    }

    public void Execute()
    {
        _collection.Add(_annotation);
    }

    public void Undo()
    {
        _collection.Remove(_annotation);
    }
}

public class DeleteAnnotationCommand : ICommand
{
    private readonly AnnotationCollection _collection;
    private readonly IAnnotation _annotation;
    private int _index;

    public DeleteAnnotationCommand(AnnotationCollection collection, IAnnotation annotation)
    {
        _collection = collection;
        _annotation = annotation;
    }

    public void Execute()
    {
        _index = _collection.IndexOf(_annotation);
        _collection.Remove(_annotation);
    }

    public void Undo()
    {
        _collection.Insert(_index, _annotation);
    }
}

public class MoveAnnotationCommand : ICommand
{
    private readonly IAnnotation _annotation;
    private readonly Point _delta;
    private Point _previousDelta;

    public MoveAnnotationCommand(IAnnotation annotation, Point delta)
    {
        _annotation = annotation;
        _delta = delta;
    }

    public void Execute()
    {
        _previousDelta = new Point(-_delta.X, -_delta.Y);
        _annotation.Move(_delta);
    }

    public void Undo()
    {
        _annotation.Move(_previousDelta);
    }
}

public class ResizeAnnotationCommand : ICommand
{
    private readonly IAnnotation _annotation;
    private readonly Size _newSize;
    private Size _oldSize;

    public ResizeAnnotationCommand(IAnnotation annotation, Size newSize)
    {
        _annotation = annotation;
        _newSize = newSize;
        _oldSize = annotation.Bounds.Size;
    }

    public void Execute()
    {
        _annotation.Resize(_newSize);
    }

    public void Undo()
    {
        _annotation.Resize(_oldSize);
    }
}

public class ChangeColorCommand : ICommand
{
    private readonly IAnnotation _annotation;
    private readonly Color _newColor;
    private Color _oldColor;

    public ChangeColorCommand(IAnnotation annotation, Color newColor)
    {
        _annotation = annotation;
        _newColor = newColor;
        _oldColor = annotation.Color;
    }

    public void Execute()
    {
        _annotation.Color = _newColor;
    }

    public void Undo()
    {
        _annotation.Color = _oldColor;
    }
}

public class ChangeStrokeWidthCommand : ICommand
{
    private readonly IAnnotation _annotation;
    private readonly int _newWidth;
    private int _oldWidth;

    public ChangeStrokeWidthCommand(IAnnotation annotation, int newWidth)
    {
        _annotation = annotation;
        _newWidth = newWidth;
        _oldWidth = annotation.StrokeWidth;
    }

    public void Execute()
    {
        _annotation.StrokeWidth = _newWidth;
    }

    public void Undo()
    {
        _annotation.StrokeWidth = _oldWidth;
    }
}

public class ChangeFontSizeCommand : ICommand
{
    private readonly TextAnnotation _annotation;
    private readonly float _newSize;
    private float _oldSize;

    public ChangeFontSizeCommand(TextAnnotation annotation, float newSize)
    {
        _annotation = annotation;
        _newSize = newSize;
        _oldSize = annotation.Font.Size;
    }

    public void Execute()
    {
        var oldFont = _annotation.Font;
        _annotation.Font = new Font(oldFont.FontFamily, _newSize, oldFont.Style);
        oldFont.Dispose();
    }

    public void Undo()
    {
        var currentFont = _annotation.Font;
        _annotation.Font = new Font(currentFont.FontFamily, _oldSize, currentFont.Style);
        currentFont.Dispose();
    }
}

public class ChangeFontCommand : ICommand
{
    private readonly TextAnnotation _annotation;
    private readonly FontFamily _newFontFamily;
    private FontFamily _oldFontFamily;

    public ChangeFontCommand(TextAnnotation annotation, FontFamily newFontFamily)
    {
        _annotation = annotation;
        _newFontFamily = newFontFamily;
        _oldFontFamily = annotation.Font.FontFamily;
    }

    public void Execute()
    {
        var oldFont = _annotation.Font;
        _annotation.Font = new Font(_newFontFamily, oldFont.Size, oldFont.Style);
        oldFont.Dispose();
    }

    public void Undo()
    {
        var currentFont = _annotation.Font;
        _annotation.Font = new Font(_oldFontFamily, currentFont.Size, currentFont.Style);
        currentFont.Dispose();
    }
}


