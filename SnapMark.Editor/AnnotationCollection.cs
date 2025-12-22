using System.Collections.ObjectModel;
using System.Drawing;

namespace SnapMark.Editor;

public class AnnotationCollection : Collection<IAnnotation>
{
    public IAnnotation? SelectedAnnotation { get; private set; }

    public void SelectAnnotation(IAnnotation? annotation)
    {
        if (SelectedAnnotation != null)
        {
            SelectedAnnotation.IsSelected = false;
        }

        SelectedAnnotation = annotation;
        if (SelectedAnnotation != null)
        {
            SelectedAnnotation.IsSelected = true;
        }
    }

    public void ClearSelection()
    {
        SelectAnnotation(null);
    }

    public IAnnotation? HitTest(Point point)
    {
        // Test from highest Z-order to lowest
        for (int i = Count - 1; i >= 0; i--)
        {
            if (this[i].HitTest(point))
            {
                return this[i];
            }
        }
        return null;
    }

    public void BringToFront(IAnnotation annotation)
    {
        if (Remove(annotation))
        {
            Add(annotation);
            UpdateZOrders();
        }
    }

    public void SendToBack(IAnnotation annotation)
    {
        if (Remove(annotation))
        {
            Insert(0, annotation);
            UpdateZOrders();
        }
    }

    private void UpdateZOrders()
    {
        for (int i = 0; i < Count; i++)
        {
            this[i].ZOrder = i;
        }
    }
}


