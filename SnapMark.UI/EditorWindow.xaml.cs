using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using SnapMark.Capture;
using SnapMark.Editor;
using System.Drawing;
using System.Drawing.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT.Interop;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SnapMark.UI;

public sealed partial class EditorWindow : Window
{
    private CaptureResult? _captureResult;
    private AnnotationCollection _annotations = new();
    private CommandManager _commandManager = new();
    private AnnotationType _currentTool = AnnotationType.Arrow;
    private Windows.Foundation.Point? _dragStart;
    private bool _isDrawing = false;
    private Microsoft.UI.Xaml.Shapes.Shape? _previewShape;
    
    // Hand tool state
    private bool _isPanning = false;
    private Windows.Foundation.Point _panStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
    
    // Selection and move state
    private bool _isMoving = false;
    private Windows.Foundation.Point? _moveStartPoint;
    private IAnnotation? _moveStartAnnotation;
    
    // Resize state
    private bool _isResizing = false;
    private ResizeHandle? _resizeHandle;
    private System.Drawing.Rectangle _resizeStartBounds;
    
    // Zoom state - prevent auto-centering when user zooms manually
    private bool _isInitialFit = true;
    private bool _isUserZooming = false;

    public EditorWindow()
    {
        this.InitializeComponent();
        this.Title = "SnapMark Editor";
        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
        this.Closed += EditorWindow_Closed;
        
        // Handle keyboard shortcuts via UIElement
        EditorCanvas.KeyDown += EditorWindow_KeyDown;
        AnnotationCanvas.KeyDown += EditorWindow_KeyDown;
        
        // Handle zoom events to preserve viewport position
        ImageScrollViewer.ViewChanged += ImageScrollViewer_ViewChanged;
    }

    public void LoadCapture(CaptureResult captureResult)
    {
        _captureResult = captureResult;
        
        // Convert Bitmap to WinUI Image
        var bitmap = captureResult.Bitmap;
        var stream = new System.IO.MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        
        var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
        var randomAccessStream = stream.AsRandomAccessStream();
        bitmapImage.SetSource(randomAccessStream);
        BackgroundImage.Source = bitmapImage;
        
        BackgroundImage.Width = bitmap.Width;
        BackgroundImage.Height = bitmap.Height;
        
        EditorCanvas.Width = bitmap.Width;
        EditorCanvas.Height = bitmap.Height;
        AnnotationCanvas.Width = bitmap.Width;
        AnnotationCanvas.Height = bitmap.Height;
        
        // Calculate and set initial zoom to fit the image in the viewport
        _isInitialFit = true;
        FitImageToViewport();
        
        RedrawAnnotations();
    }

    private void FitImageToViewport()
    {
        if (_captureResult == null) return;

        // Use a one-time layout updated handler
        EventHandler<object>? layoutHandler = null;
        layoutHandler = (s, e) =>
        {
            ImageScrollViewer.LayoutUpdated -= layoutHandler;
            if (_isInitialFit)
            {
                CalculateAndSetZoom();
            }
        };
        ImageScrollViewer.LayoutUpdated += layoutHandler;
        
        // Also try immediately in case layout is already done
        this.DispatcherQueue.TryEnqueue(() =>
        {
            if (ImageScrollViewer.ActualWidth > 0 && ImageScrollViewer.ActualHeight > 0 && _isInitialFit)
            {
                CalculateAndSetZoom();
            }
        });
    }

    private void CalculateAndSetZoom()
    {
        if (_captureResult == null) return;

        var bitmap = _captureResult.Bitmap;
        var viewportWidth = ImageScrollViewer.ActualWidth;
        var viewportHeight = ImageScrollViewer.ActualHeight;

        // Account for some padding/margin
        const double padding = 20.0;
        var availableWidth = Math.Max(100, viewportWidth - padding);
        var availableHeight = Math.Max(100, viewportHeight - padding);

        if (availableWidth <= 0 || availableHeight <= 0) return;

        // Calculate scale factors to fit both width and height
        var scaleX = availableWidth / bitmap.Width;
        var scaleY = availableHeight / bitmap.Height;
        
        // Use the smaller scale to ensure the image fits completely
        var zoomFactor = Math.Min(scaleX, scaleY);
        
        // Clamp to reasonable bounds
        zoomFactor = Math.Max(0.1, Math.Min(1.0, zoomFactor));
        
        // Only center on initial fit, not on user zoom
        if (_isInitialFit)
        {
            ImageScrollViewer.ZoomToFactor((float)zoomFactor);
            
            // Center the image only on initial load
            ImageScrollViewer.ChangeView(
                (bitmap.Width * zoomFactor - viewportWidth) / 2,
                (bitmap.Height * zoomFactor - viewportHeight) / 2,
                (float)zoomFactor);
            
            _isInitialFit = false;
        }
    }
    
    private void ImageScrollViewer_ViewChanged(object? sender, Microsoft.UI.Xaml.Controls.ScrollViewerViewChangedEventArgs e)
    {
        // Mark that user is interacting with zoom/pan, so we don't auto-center
        if (!e.IsIntermediate)
        {
            _isInitialFit = false;
        }
    }

    private void SelectorToolButton_Click(object sender, RoutedEventArgs e)
    {
        _currentTool = AnnotationType.Selector;
        UpdateToolButtons();
        UpdateCursor();
    }

    private void HandToolButton_Click(object sender, RoutedEventArgs e)
    {
        _currentTool = AnnotationType.Hand;
        UpdateToolButtons();
        UpdateCursor();
    }

    private void ArrowToolButton_Click(object sender, RoutedEventArgs e)
    {
        _currentTool = AnnotationType.Arrow;
        UpdateToolButtons();
        UpdateCursor();
    }

    private void RectangleToolButton_Click(object sender, RoutedEventArgs e)
    {
        _currentTool = AnnotationType.Rectangle;
        UpdateToolButtons();
        UpdateCursor();
    }

    private void LineToolButton_Click(object sender, RoutedEventArgs e)
    {
        _currentTool = AnnotationType.Line;
        UpdateToolButtons();
        UpdateCursor();
    }

    private void TextToolButton_Click(object sender, RoutedEventArgs e)
    {
        _currentTool = AnnotationType.Text;
        UpdateToolButtons();
        UpdateCursor();
    }

    private void UpdateToolButtons()
    {
        // Visual feedback - highlight active tool
        SelectorToolButton.Style = _currentTool == AnnotationType.Selector ? GetHighlightedButtonStyle() : null;
        HandToolButton.Style = _currentTool == AnnotationType.Hand ? GetHighlightedButtonStyle() : null;
        ArrowToolButton.Style = _currentTool == AnnotationType.Arrow ? GetHighlightedButtonStyle() : null;
        RectangleToolButton.Style = _currentTool == AnnotationType.Rectangle ? GetHighlightedButtonStyle() : null;
        LineToolButton.Style = _currentTool == AnnotationType.Line ? GetHighlightedButtonStyle() : null;
        TextToolButton.Style = _currentTool == AnnotationType.Text ? GetHighlightedButtonStyle() : null;
    }

    private void UpdateCursor()
    {
        // Note: ProtectedCursor requires WinUI 3.1+ and may not be available
        // Cursor will be handled by the UI framework automatically
        // For now, we'll rely on default cursor behavior
    }

    private Microsoft.UI.Xaml.Style? GetHighlightedButtonStyle()
    {
        // Simple highlighting - in production, use proper Style resource
        return null; // Will implement with proper styling
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        _ = CopyToClipboard();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveToFile();
    }

    private Task CopyToClipboard()
    {
        try
        {
            var bitmap = RenderAnnotations();
            var stream = new System.IO.MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;

            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            var ras = stream.AsRandomAccessStream();
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(ras));
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        }
        catch
        {
            // Silently fail - in production, show error notification
        }
        return Task.CompletedTask;
    }

    private async Task SaveToFile()
    {
        try
        {
            var picker = new FileSavePicker();
            picker.SuggestedFileName = $"Screenshot {DateTime.Now:yyyy-MM-dd HH-mm-ss}";
            picker.FileTypeChoices.Add("PNG Image", new[] { ".png" });
            picker.FileTypeChoices.Add("JPEG Image", new[] { ".jpg" });

            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var renderedBitmap = RenderAnnotations();
                var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
                
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                    file.FileType == ".jpg" 
                        ? Windows.Graphics.Imaging.BitmapEncoder.JpegEncoderId 
                        : Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                    stream);
                
                encoder.SetSoftwareBitmap(await ConvertToSoftwareBitmap(renderedBitmap));
                await encoder.FlushAsync();
            }
        }
        catch
        {
            // Silently fail - in production, show error notification
        }
    }

    private Bitmap RenderAnnotations()
    {
        if (_captureResult == null)
            throw new InvalidOperationException("No capture loaded");

        var bitmap = new Bitmap(_captureResult.Bitmap);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        foreach (var annotation in _annotations)
        {
            annotation.Draw(graphics);
        }

        return bitmap;
    }

    private void EditorWindow_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            this.Close();
        }
        else if (e.Key == Windows.System.VirtualKey.Enter)
        {
            CopyToClipboard();
        }
        else if (e.Key == Windows.System.VirtualKey.S)
        {
            var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            if (ctrlState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            {
                _ = SaveToFile();
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Z)
        {
            var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            if (ctrlState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            {
                _commandManager.Undo();
                RedrawAnnotations();
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Y)
        {
            var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            if (ctrlState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            {
                _commandManager.Redo();
                RedrawAnnotations();
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Delete)
        {
            if (_annotations.SelectedAnnotation != null)
            {
                var command = new DeleteAnnotationCommand(_annotations, _annotations.SelectedAnnotation);
                _commandManager.ExecuteCommand(command);
                RedrawAnnotations();
            }
        }
    }

    private void EditorWindow_Closed(object sender, WindowEventArgs args)
    {
        _commandManager.Clear();
        _annotations.Clear();
    }

    private void ImageScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Handle hand tool panning on ScrollViewer
        if (_currentTool == AnnotationType.Hand)
        {
            var point = e.GetCurrentPoint(ImageScrollViewer).Position;
            _isPanning = true;
            _panStartPoint = point;
            _panStartHorizontalOffset = ImageScrollViewer.HorizontalOffset;
            _panStartVerticalOffset = ImageScrollViewer.VerticalOffset;
            ImageScrollViewer.CapturePointer(e.Pointer);
            e.Handled = true;
        }
        else
        {
            // For other tools, don't capture - let child elements handle it
            e.Handled = false;
        }
    }

    private void ImageScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        // Handle panning
        if (_isPanning && _currentTool == AnnotationType.Hand)
        {
            var currentPoint = e.GetCurrentPoint(ImageScrollViewer).Position;
            var deltaX = _panStartPoint.X - currentPoint.X;
            var deltaY = _panStartPoint.Y - currentPoint.Y;
            
            var newHorizontalOffset = _panStartHorizontalOffset + deltaX;
            var newVerticalOffset = _panStartVerticalOffset + deltaY;
            
            ImageScrollViewer.ChangeView(newHorizontalOffset, newVerticalOffset, null);
            e.Handled = true;
        }
        else
        {
            e.Handled = false;
        }
    }

    private void ImageScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        // Handle panning end (for clicks directly on ScrollViewer, not on child elements)
        if (_isPanning && _currentTool == AnnotationType.Hand)
        {
            _isPanning = false;
            ImageScrollViewer.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
        else
        {
            e.Handled = false;
        }
    }
    
    private void ImageScrollViewer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // Update cursor when hand tool is active
        if (_currentTool == AnnotationType.Hand)
        {
            // Set hand cursor - WinUI 3 uses ProtectedCursor
            try
            {
                var cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
                ImageScrollViewer.ProtectedCursor = cursor;
            }
            catch
            {
                // Fallback if ProtectedCursor is not available
            }
        }
    }
    
    private void ImageScrollViewer_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        // Reset cursor when leaving ScrollViewer
        try
        {
            ImageScrollViewer.ProtectedCursor = null;
        }
        catch
        {
            // Ignore if ProtectedCursor is not available
        }
    }

    private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_annotations.SelectedAnnotation == null) return;
        
        var flyout = new Flyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom
        };
        
        var colorGrid = new Grid();
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        colorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        colorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        colorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        colorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        
        var colors = new[]
        {
            System.Drawing.Color.Red, System.Drawing.Color.Blue, System.Drawing.Color.Green, System.Drawing.Color.Yellow,
            System.Drawing.Color.Orange, System.Drawing.Color.Purple, System.Drawing.Color.Pink, System.Drawing.Color.Cyan,
            System.Drawing.Color.Black, System.Drawing.Color.White, System.Drawing.Color.Gray, System.Drawing.Color.Brown,
            System.Drawing.Color.Magenta, System.Drawing.Color.Lime, System.Drawing.Color.Navy, System.Drawing.Color.Teal
        };
        
        for (int i = 0; i < colors.Length; i++)
        {
            var colorButton = new Button
            {
                Background = ColorToBrush(colors[i]),
                Margin = new Thickness(2)
            };
            
            var color = colors[i];
            colorButton.Click += (s, args) =>
            {
                if (_annotations.SelectedAnnotation != null)
                {
                    var command = new ChangeColorCommand(_annotations.SelectedAnnotation, color);
                    _commandManager.ExecuteCommand(command);
                    UpdatePropertyPanel();
                    RedrawAnnotations();
                }
                flyout.Hide();
            };
            
            Grid.SetRow(colorButton, i / 4);
            Grid.SetColumn(colorButton, i % 4);
            colorGrid.Children.Add(colorButton);
        }
        
        flyout.Content = colorGrid;
        flyout.ShowAt(sender as FrameworkElement);
    }

    private void StrokeWidthSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_annotations.SelectedAnnotation == null) return;
        
        var newWidth = (int)e.NewValue;
        StrokeWidthText.Text = newWidth.ToString();
        
        // Update annotation stroke width via command
        var command = new ChangeStrokeWidthCommand(_annotations.SelectedAnnotation, newWidth);
        _commandManager.ExecuteCommand(command);
        RedrawAnnotations();
    }

    private void UpdatePropertyPanel()
    {
        if (_annotations.SelectedAnnotation != null)
        {
            PropertyPanel.Visibility = Visibility.Visible;
            ColorSwatch.Color = Windows.UI.Color.FromArgb(
                _annotations.SelectedAnnotation.Color.A,
                _annotations.SelectedAnnotation.Color.R,
                _annotations.SelectedAnnotation.Color.G,
                _annotations.SelectedAnnotation.Color.B);
            StrokeWidthSlider.Value = _annotations.SelectedAnnotation.StrokeWidth;
            StrokeWidthText.Text = _annotations.SelectedAnnotation.StrokeWidth.ToString();
        }
        else
        {
            PropertyPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void AnnotationCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Handle hand tool panning - need to handle here because AnnotationCanvas captures events
        if (_currentTool == AnnotationType.Hand)
        {
            // Get point relative to ScrollViewer for panning
            var scrollViewerPoint = e.GetCurrentPoint(ImageScrollViewer).Position;
            _isPanning = true;
            _panStartPoint = scrollViewerPoint;
            _panStartHorizontalOffset = ImageScrollViewer.HorizontalOffset;
            _panStartVerticalOffset = ImageScrollViewer.VerticalOffset;
            AnnotationCanvas.CapturePointer(e.Pointer);
            e.Handled = true;
            return;
        }
        
        var point = e.GetCurrentPoint(AnnotationCanvas).Position;
        
        // Handle selector tool or default selection mode
        if (_currentTool == AnnotationType.Selector)
        {
            var drawingPoint = WinUIToDrawingPoint(point);
            var hitAnnotation = _annotations.HitTest(drawingPoint);
            
            if (hitAnnotation != null)
            {
                // Check if clicking on resize handle
                var handle = GetResizeHandle(hitAnnotation, drawingPoint);
                if (handle != ResizeHandle.None)
                {
                    _isResizing = true;
                    _resizeHandle = handle;
                    _resizeStartBounds = hitAnnotation.Bounds;
                    _annotations.SelectAnnotation(hitAnnotation);
                    UpdatePropertyPanel();
                    AnnotationCanvas.CapturePointer(e.Pointer);
                    return;
                }
                
                // Start moving annotation
                _isMoving = true;
                _moveStartPoint = point;
                _moveStartAnnotation = hitAnnotation;
                _annotations.SelectAnnotation(hitAnnotation);
                UpdatePropertyPanel();
                AnnotationCanvas.CapturePointer(e.Pointer);
                RedrawAnnotations();
                return;
            }
            
            // Clear selection if clicking on empty space
            if (_annotations.SelectedAnnotation != null)
            {
                _annotations.ClearSelection();
                UpdatePropertyPanel();
                RedrawAnnotations();
            }
            return;
        }
        
        // Handle text tool
        if (_currentTool == AnnotationType.Text)
        {
            // For text, show input dialog
            ShowTextInputDialog(point);
            return;
        }

        // Handle drawing tools (Arrow, Rectangle, Line)
        // Start drawing new annotation
        _isDrawing = true;
        _dragStart = point;
        AnnotationCanvas.CapturePointer(e.Pointer);
    }

    private void AnnotationCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        // Handle panning when hand tool is active
        if (_isPanning && _currentTool == AnnotationType.Hand)
        {
            // Get point relative to ScrollViewer for panning
            var currentScrollViewerPoint = e.GetCurrentPoint(ImageScrollViewer).Position;
            var deltaX = _panStartPoint.X - currentScrollViewerPoint.X;
            var deltaY = _panStartPoint.Y - currentScrollViewerPoint.Y;
            
            var newHorizontalOffset = _panStartHorizontalOffset + deltaX;
            var newVerticalOffset = _panStartVerticalOffset + deltaY;
            
            ImageScrollViewer.ChangeView(newHorizontalOffset, newVerticalOffset, null);
            e.Handled = true;
            return;
        }
        
        var currentPoint = e.GetCurrentPoint(AnnotationCanvas).Position;
        
        // Handle resizing
        if (_isResizing && _moveStartAnnotation != null && _resizeHandle.HasValue)
        {
            var currentDrawingPoint = WinUIToDrawingPoint(currentPoint);
            ResizeAnnotation(_moveStartAnnotation, _resizeHandle.Value, currentDrawingPoint);
            RedrawAnnotations();
            return;
        }
        
        // Handle moving
        if (_isMoving && _moveStartPoint.HasValue && _moveStartAnnotation != null)
        {
            var deltaX = currentPoint.X - _moveStartPoint.Value.X;
            var deltaY = currentPoint.Y - _moveStartPoint.Value.Y;
            
            // Convert delta to canvas coordinates accounting for zoom
            var zoomFactor = ImageScrollViewer.ZoomFactor;
            var drawingDelta = new System.Drawing.Point((int)(deltaX / zoomFactor), (int)(deltaY / zoomFactor));
            
            // Temporarily move annotation for preview
            var originalBounds = _moveStartAnnotation.Bounds;
            _moveStartAnnotation.Move(drawingDelta);
            RedrawAnnotations();
            
            // Restore original bounds (will be set properly on release)
            _moveStartAnnotation.Bounds = originalBounds;
            _moveStartAnnotation.Move(new System.Drawing.Point(-drawingDelta.X, -drawingDelta.Y));
            _moveStartAnnotation.Move(drawingDelta);
            return;
        }
        
        // Handle drawing preview
        if (_isDrawing && _dragStart.HasValue)
        {
            UpdatePreviewShape(_dragStart.Value, currentPoint);
        }
    }

    private void AnnotationCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        // Handle panning end
        if (_isPanning && _currentTool == AnnotationType.Hand)
        {
            _isPanning = false;
            AnnotationCanvas.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
            return;
        }
        
        // Handle resize end
        if (_isResizing && _moveStartAnnotation != null && _resizeHandle.HasValue)
        {
            var endPoint = e.GetCurrentPoint(AnnotationCanvas).Position;
            var endDrawingPoint = WinUIToDrawingPoint(endPoint);
            ResizeAnnotation(_moveStartAnnotation, _resizeHandle.Value, endDrawingPoint);
            
            // Create resize command
            var newSize = _moveStartAnnotation.Bounds.Size;
            var command = new ResizeAnnotationCommand(_moveStartAnnotation, newSize);
            _commandManager.ExecuteCommand(command);
            
            _isResizing = false;
            _resizeHandle = null;
            _moveStartAnnotation = null;
            AnnotationCanvas.ReleasePointerCapture(e.Pointer);
            RedrawAnnotations();
            return;
        }
        
        // Handle move end
        if (_isMoving && _moveStartPoint.HasValue && _moveStartAnnotation != null)
        {
            var endPoint = e.GetCurrentPoint(AnnotationCanvas).Position;
            var deltaX = endPoint.X - _moveStartPoint.Value.X;
            var deltaY = endPoint.Y - _moveStartPoint.Value.Y;
            
            // Convert delta to canvas coordinates accounting for zoom
            var zoomFactor = ImageScrollViewer.ZoomFactor;
            var drawingDelta = new System.Drawing.Point((int)(deltaX / zoomFactor), (int)(deltaY / zoomFactor));
            
            if (drawingDelta.X != 0 || drawingDelta.Y != 0)
            {
                var command = new MoveAnnotationCommand(_moveStartAnnotation, drawingDelta);
                _commandManager.ExecuteCommand(command);
            }
            
            _isMoving = false;
            _moveStartPoint = null;
            _moveStartAnnotation = null;
            AnnotationCanvas.ReleasePointerCapture(e.Pointer);
            RedrawAnnotations();
            return;
        }
        
        // Handle drawing end
        if (_isDrawing && _dragStart.HasValue)
        {
            var endPoint = e.GetCurrentPoint(AnnotationCanvas).Position;
            CreateAnnotation(_dragStart.Value, endPoint);
            
            _isDrawing = false;
            _dragStart = null;
            ClearPreviewShape();
            AnnotationCanvas.ReleasePointerCapture(e.Pointer);
        }
    }

    private void UpdatePreviewShape(Windows.Foundation.Point start, Windows.Foundation.Point end)
    {
        ClearPreviewShape();

        var startPoint = new System.Drawing.Point((int)start.X, (int)start.Y);
        var endPoint = new System.Drawing.Point((int)end.X, (int)end.Y);

        switch (_currentTool)
        {
            case AnnotationType.Arrow:
                _previewShape = CreateArrowPreview(start, end);
                break;
            case AnnotationType.Rectangle:
                _previewShape = CreateRectanglePreview(start, end);
                break;
            case AnnotationType.Line:
                _previewShape = CreateLinePreview(start, end);
                break;
        }

        if (_previewShape != null)
        {
            AnnotationCanvas.Children.Add(_previewShape);
        }
    }

    private Microsoft.UI.Xaml.Shapes.Shape CreateArrowPreview(Windows.Foundation.Point start, Windows.Foundation.Point end)
    {
        var line = new Microsoft.UI.Xaml.Shapes.Line
        {
            X1 = start.X,
            Y1 = start.Y,
            X2 = end.X,
            Y2 = end.Y,
            Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
            StrokeThickness = 2
        };
        return line;
    }

    private Microsoft.UI.Xaml.Shapes.Shape CreateRectanglePreview(Windows.Foundation.Point start, Windows.Foundation.Point end)
    {
        var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = Math.Abs(end.X - start.X),
            Height = Math.Abs(end.Y - start.Y),
            Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Blue),
            StrokeThickness = 2,
            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };
        Canvas.SetLeft(rect, Math.Min(start.X, end.X));
        Canvas.SetTop(rect, Math.Min(start.Y, end.Y));
        return rect;
    }

    private Microsoft.UI.Xaml.Shapes.Shape CreateLinePreview(Windows.Foundation.Point start, Windows.Foundation.Point end)
    {
        var line = new Microsoft.UI.Xaml.Shapes.Line
        {
            X1 = start.X,
            Y1 = start.Y,
            X2 = end.X,
            Y2 = end.Y,
            Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green),
            StrokeThickness = 2
        };
        return line;
    }

    private void ClearPreviewShape()
    {
        if (_previewShape != null)
        {
            AnnotationCanvas.Children.Remove(_previewShape);
            _previewShape = null;
        }
    }

    private void CreateAnnotation(Windows.Foundation.Point start, Windows.Foundation.Point end)
    {
        var startPoint = new System.Drawing.Point((int)start.X, (int)start.Y);
        var endPoint = new System.Drawing.Point((int)end.X, (int)end.Y);

        IAnnotation? annotation = _currentTool switch
        {
            AnnotationType.Arrow => new ArrowAnnotation(startPoint, endPoint),
            AnnotationType.Rectangle => new RectangleAnnotation(new System.Drawing.Rectangle(
                Math.Min(startPoint.X, endPoint.X),
                Math.Min(startPoint.Y, endPoint.Y),
                Math.Abs(endPoint.X - startPoint.X),
                Math.Abs(endPoint.Y - startPoint.Y))),
            AnnotationType.Line => new LineAnnotation(startPoint, endPoint),
            _ => null
        };

        if (annotation != null)
        {
            var command = new CreateAnnotationCommand(_annotations, annotation);
            _commandManager.ExecuteCommand(command);
            RedrawAnnotations();
        }
    }

    private async void ShowTextInputDialog(Windows.Foundation.Point position)
    {
        var dialog = new ContentDialog
        {
            Title = "Enter Text",
            PrimaryButtonText = "OK",
            SecondaryButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };
        
        var textBox = new TextBox
        {
            PlaceholderText = "Enter annotation text...",
            MinWidth = 300,
            AcceptsReturn = false
        };
        
        dialog.Content = textBox;
        
        // Focus the text box when dialog opens
        dialog.Opened += (s, e) => textBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        
        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            var drawingPoint = WinUIToDrawingPoint(position);
            var textAnnotation = new TextAnnotation(drawingPoint, textBox.Text);
            var command = new CreateAnnotationCommand(_annotations, textAnnotation);
            _commandManager.ExecuteCommand(command);
            RedrawAnnotations();
        }
    }

    private void RedrawAnnotations()
    {
        // Clear existing annotation shapes and handles
        var shapesToRemove = AnnotationCanvas.Children
            .OfType<Microsoft.UI.Xaml.Shapes.Shape>()
            .Where(s => s != _previewShape)
            .ToList();
        
        foreach (var shape in shapesToRemove)
        {
            AnnotationCanvas.Children.Remove(shape);
        }

        // Render annotations as WinUI shapes
        foreach (var annotation in _annotations)
        {
            var shape = AnnotationToShape(annotation);
            if (shape != null)
            {
                AnnotationCanvas.Children.Add(shape);
            }
        }
        
        // Draw selection indicators and resize handles for selected annotation
        if (_annotations.SelectedAnnotation != null)
        {
            DrawSelectionHandles(_annotations.SelectedAnnotation);
        }
    }

    private void DrawSelectionHandles(IAnnotation annotation)
    {
        const int handleSize = 8;
        const int handleHalfSize = handleSize / 2;
        var bounds = annotation.Bounds;
        var handleBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Blue);
        
        // Draw 8 resize handles (corners + edges)
        var handles = new[]
        {
            new { Point = new Windows.Foundation.Point(bounds.Left, bounds.Top), Handle = ResizeHandle.TopLeft },
            new { Point = new Windows.Foundation.Point(bounds.Left + bounds.Width / 2.0, bounds.Top), Handle = ResizeHandle.Top },
            new { Point = new Windows.Foundation.Point(bounds.Right, bounds.Top), Handle = ResizeHandle.TopRight },
            new { Point = new Windows.Foundation.Point(bounds.Right, bounds.Top + bounds.Height / 2.0), Handle = ResizeHandle.Right },
            new { Point = new Windows.Foundation.Point(bounds.Right, bounds.Bottom), Handle = ResizeHandle.BottomRight },
            new { Point = new Windows.Foundation.Point(bounds.Left + bounds.Width / 2.0, bounds.Bottom), Handle = ResizeHandle.Bottom },
            new { Point = new Windows.Foundation.Point(bounds.Left, bounds.Bottom), Handle = ResizeHandle.BottomLeft },
            new { Point = new Windows.Foundation.Point(bounds.Left, bounds.Top + bounds.Height / 2.0), Handle = ResizeHandle.Left }
        };
        
        foreach (var handle in handles)
        {
            var winUIPoint = DrawingToWinUIPoint(new System.Drawing.Point((int)handle.Point.X, (int)handle.Point.Y));
            var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = handleSize,
                Height = handleSize,
                Fill = handleBrush,
                Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                StrokeThickness = 1
            };
            Canvas.SetLeft(rect, winUIPoint.X - handleHalfSize);
            Canvas.SetTop(rect, winUIPoint.Y - handleHalfSize);
            AnnotationCanvas.Children.Add(rect);
        }
        
        // Draw selection rectangle
        var selectionRect = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = bounds.Width,
            Height = bounds.Height,
            Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Blue),
            StrokeThickness = 2,
            StrokeDashArray = new Microsoft.UI.Xaml.Media.DoubleCollection { 4, 4 },
            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };
        var topLeftWinUI = DrawingToWinUIPoint(new System.Drawing.Point(bounds.Left, bounds.Top));
        Canvas.SetLeft(selectionRect, topLeftWinUI.X);
        Canvas.SetTop(selectionRect, topLeftWinUI.Y);
        AnnotationCanvas.Children.Add(selectionRect);
    }

    private Microsoft.UI.Xaml.Shapes.Shape? AnnotationToShape(IAnnotation annotation)
    {
        return annotation switch
        {
            ArrowAnnotation arrow => ArrowToShape(arrow),
            RectangleAnnotation rect => RectangleToShape(rect),
            LineAnnotation line => LineToShape(line),
            TextAnnotation text => TextToShape(text),
            _ => null
        };
    }

    private Microsoft.UI.Xaml.Shapes.Shape ArrowToShape(ArrowAnnotation arrow)
    {
        // Calculate arrow direction and length
        var dx = arrow.EndPoint.X - arrow.StartPoint.X;
        var dy = arrow.EndPoint.Y - arrow.StartPoint.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        
        if (length == 0)
        {
            // Degenerate case - just draw a point
            var ellipse = new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = arrow.StrokeWidth,
                Height = arrow.StrokeWidth,
                Fill = ColorToBrush(arrow.Color)
            };
            Canvas.SetLeft(ellipse, arrow.StartPoint.X - arrow.StrokeWidth / 2.0);
            Canvas.SetTop(ellipse, arrow.StartPoint.Y - arrow.StrokeWidth / 2.0);
            return ellipse;
        }
        
        // Normalize direction vector
        var unitX = dx / length;
        var unitY = dy / length;
        
        // Arrowhead size scales with stroke width
        var arrowheadLength = Math.Max(arrow.StrokeWidth * 3, 10);
        var arrowheadWidth = arrowheadLength * 0.6;
        
        // Perpendicular vector for arrowhead base
        var perpX = -unitY;
        var perpY = unitX;
        
        // Arrowhead points
        var tipX = arrow.EndPoint.X;
        var tipY = arrow.EndPoint.Y;
        var baseX = tipX - unitX * arrowheadLength;
        var baseY = tipY - unitY * arrowheadLength;
        var leftX = baseX + perpX * arrowheadWidth / 2;
        var leftY = baseY + perpY * arrowheadWidth / 2;
        var rightX = baseX - perpX * arrowheadWidth / 2;
        var rightY = baseY - perpY * arrowheadWidth / 2;
        
        // Create Path geometry for arrow (line + arrowhead triangle)
        var pathGeometry = new Microsoft.UI.Xaml.Media.PathGeometry();
        var pathFigure = new Microsoft.UI.Xaml.Media.PathFigure
        {
            StartPoint = new Windows.Foundation.Point(arrow.StartPoint.X, arrow.StartPoint.Y),
            IsClosed = false
        };
        
        // Line segment to arrowhead base
        pathFigure.Segments.Add(new Microsoft.UI.Xaml.Media.LineSegment
        {
            Point = new Windows.Foundation.Point(baseX, baseY)
        });
        
        // Arrowhead triangle
        pathFigure.Segments.Add(new Microsoft.UI.Xaml.Media.LineSegment
        {
            Point = new Windows.Foundation.Point(leftX, leftY)
        });
        pathFigure.Segments.Add(new Microsoft.UI.Xaml.Media.LineSegment
        {
            Point = new Windows.Foundation.Point(tipX, tipY)
        });
        pathFigure.Segments.Add(new Microsoft.UI.Xaml.Media.LineSegment
        {
            Point = new Windows.Foundation.Point(rightX, rightY)
        });
        pathFigure.Segments.Add(new Microsoft.UI.Xaml.Media.LineSegment
        {
            Point = new Windows.Foundation.Point(baseX, baseY)
        });
        
        pathGeometry.Figures.Add(pathFigure);
        
        var path = new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = pathGeometry,
            Stroke = ColorToBrush(arrow.Color),
            Fill = ColorToBrush(arrow.Color),
            StrokeThickness = arrow.StrokeWidth,
            StrokeLineJoin = Microsoft.UI.Xaml.Media.PenLineJoin.Round,
            StrokeEndLineCap = Microsoft.UI.Xaml.Media.PenLineCap.Round
        };
        
        return path;
    }

    private Microsoft.UI.Xaml.Shapes.Shape RectangleToShape(RectangleAnnotation rect)
    {
        var shape = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = rect.Bounds.Width,
            Height = rect.Bounds.Height,
            Stroke = ColorToBrush(rect.Color),
            StrokeThickness = rect.StrokeWidth,
            Fill = rect.IsFilled ? ColorToBrush(rect.Color) : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };
        Canvas.SetLeft(shape, rect.Bounds.X);
        Canvas.SetTop(shape, rect.Bounds.Y);
        return shape;
    }

    private Microsoft.UI.Xaml.Shapes.Shape LineToShape(LineAnnotation line)
    {
        var shape = new Microsoft.UI.Xaml.Shapes.Line
        {
            X1 = line.StartPoint.X,
            Y1 = line.StartPoint.Y,
            X2 = line.EndPoint.X,
            Y2 = line.EndPoint.Y,
            Stroke = ColorToBrush(line.Color),
            StrokeThickness = line.StrokeWidth
        };
        return shape;
    }

    private Microsoft.UI.Xaml.Shapes.Shape? TextToShape(TextAnnotation text)
    {
        // Text requires TextBlock, not Shape
        var textBlock = new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Text = text.Text,
            Foreground = ColorToBrush(text.Color),
            FontSize = text.Font.Size
        };
        Canvas.SetLeft(textBlock, text.Bounds.X);
        Canvas.SetTop(textBlock, text.Bounds.Y);
        AnnotationCanvas.Children.Add(textBlock);
        return null; // TextBlock is not a Shape
    }

    private Microsoft.UI.Xaml.Media.Brush ColorToBrush(System.Drawing.Color color)
    {
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B));
    }

    private async Task<Windows.Graphics.Imaging.SoftwareBitmap> ConvertToSoftwareBitmap(Bitmap bitmap)
    {
        var memStream = new MemoryStream();
        bitmap.Save(memStream, ImageFormat.Png);
        memStream.Position = 0;
        
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(memStream.AsRandomAccessStream());
        return await decoder.GetSoftwareBitmapAsync();
    }

    // Coordinate conversion helpers (Task 8)
    private System.Drawing.Point WinUIToDrawingPoint(Windows.Foundation.Point winUIPoint)
    {
        var zoomFactor = ImageScrollViewer.ZoomFactor;
        var horizontalOffset = ImageScrollViewer.HorizontalOffset;
        var verticalOffset = ImageScrollViewer.VerticalOffset;
        
        // Convert from ScrollViewer coordinates to canvas coordinates
        var canvasX = (winUIPoint.X + horizontalOffset) / zoomFactor;
        var canvasY = (winUIPoint.Y + verticalOffset) / zoomFactor;
        
        return new System.Drawing.Point((int)canvasX, (int)canvasY);
    }

    private Windows.Foundation.Point DrawingToWinUIPoint(System.Drawing.Point drawingPoint)
    {
        var zoomFactor = ImageScrollViewer.ZoomFactor;
        var horizontalOffset = ImageScrollViewer.HorizontalOffset;
        var verticalOffset = ImageScrollViewer.VerticalOffset;
        
        // Convert from canvas coordinates to ScrollViewer coordinates
        var winUIX = drawingPoint.X * zoomFactor - horizontalOffset;
        var winUIY = drawingPoint.Y * zoomFactor - verticalOffset;
        
        return new Windows.Foundation.Point(winUIX, winUIY);
    }

    private ResizeHandle GetResizeHandle(IAnnotation annotation, System.Drawing.Point point)
    {
        if (!annotation.IsSelected) return ResizeHandle.None;
        
        const int handleSize = 8;
        const int handleHalfSize = handleSize / 2;
        var bounds = annotation.Bounds;
        
        // Check corners first (higher priority)
        if (Math.Abs(point.X - bounds.Left) <= handleHalfSize && Math.Abs(point.Y - bounds.Top) <= handleHalfSize)
            return ResizeHandle.TopLeft;
        if (Math.Abs(point.X - bounds.Right) <= handleHalfSize && Math.Abs(point.Y - bounds.Top) <= handleHalfSize)
            return ResizeHandle.TopRight;
        if (Math.Abs(point.X - bounds.Right) <= handleHalfSize && Math.Abs(point.Y - bounds.Bottom) <= handleHalfSize)
            return ResizeHandle.BottomRight;
        if (Math.Abs(point.X - bounds.Left) <= handleHalfSize && Math.Abs(point.Y - bounds.Bottom) <= handleHalfSize)
            return ResizeHandle.BottomLeft;
        
        // Check edges
        if (Math.Abs(point.Y - bounds.Top) <= handleHalfSize && point.X >= bounds.Left && point.X <= bounds.Right)
            return ResizeHandle.Top;
        if (Math.Abs(point.X - bounds.Right) <= handleHalfSize && point.Y >= bounds.Top && point.Y <= bounds.Bottom)
            return ResizeHandle.Right;
        if (Math.Abs(point.Y - bounds.Bottom) <= handleHalfSize && point.X >= bounds.Left && point.X <= bounds.Right)
            return ResizeHandle.Bottom;
        if (Math.Abs(point.X - bounds.Left) <= handleHalfSize && point.Y >= bounds.Top && point.Y <= bounds.Bottom)
            return ResizeHandle.Left;
        
        return ResizeHandle.None;
    }

    private void ResizeAnnotation(IAnnotation annotation, ResizeHandle handle, System.Drawing.Point newPoint)
    {
        var bounds = annotation.Bounds;
        var newBounds = bounds;
        
        switch (handle)
        {
            case ResizeHandle.TopLeft:
                newBounds = new System.Drawing.Rectangle(newPoint.X, newPoint.Y, bounds.Right - newPoint.X, bounds.Bottom - newPoint.Y);
                break;
            case ResizeHandle.Top:
                newBounds = new System.Drawing.Rectangle(bounds.Left, newPoint.Y, bounds.Width, bounds.Bottom - newPoint.Y);
                break;
            case ResizeHandle.TopRight:
                newBounds = new System.Drawing.Rectangle(bounds.Left, newPoint.Y, newPoint.X - bounds.Left, bounds.Bottom - newPoint.Y);
                break;
            case ResizeHandle.Right:
                newBounds = new System.Drawing.Rectangle(bounds.Left, bounds.Top, newPoint.X - bounds.Left, bounds.Height);
                break;
            case ResizeHandle.BottomRight:
                newBounds = new System.Drawing.Rectangle(bounds.Left, bounds.Top, newPoint.X - bounds.Left, newPoint.Y - bounds.Top);
                break;
            case ResizeHandle.Bottom:
                newBounds = new System.Drawing.Rectangle(bounds.Left, bounds.Top, bounds.Width, newPoint.Y - bounds.Top);
                break;
            case ResizeHandle.BottomLeft:
                newBounds = new System.Drawing.Rectangle(newPoint.X, bounds.Top, bounds.Right - newPoint.X, newPoint.Y - bounds.Top);
                break;
            case ResizeHandle.Left:
                newBounds = new System.Drawing.Rectangle(newPoint.X, bounds.Top, bounds.Right - newPoint.X, bounds.Height);
                break;
        }
        
        // Ensure minimum size
        if (newBounds.Width < 5) newBounds.Width = 5;
        if (newBounds.Height < 5) newBounds.Height = 5;
        
        // Handle special cases for Arrow and Line
        if (annotation is ArrowAnnotation arrow)
        {
            // For arrows, resize by moving start/end points
            // Map handles to start/end points: TopLeft/Left/BottomLeft -> start, others -> end
            if (handle == ResizeHandle.TopLeft || handle == ResizeHandle.Left || handle == ResizeHandle.BottomLeft)
            {
                arrow.StartPoint = new System.Drawing.Point(newBounds.Left, newBounds.Top);
            }
            else if (handle == ResizeHandle.TopRight || handle == ResizeHandle.Right || handle == ResizeHandle.BottomRight)
            {
                arrow.EndPoint = new System.Drawing.Point(newBounds.Right, newBounds.Bottom);
            }
            else if (handle == ResizeHandle.Top)
            {
                arrow.StartPoint = new System.Drawing.Point(arrow.StartPoint.X, newBounds.Top);
            }
            else if (handle == ResizeHandle.Bottom)
            {
                arrow.EndPoint = new System.Drawing.Point(arrow.EndPoint.X, newBounds.Bottom);
            }
            // Update bounds after changing points
            arrow.Bounds = new System.Drawing.Rectangle(
                Math.Min(arrow.StartPoint.X, arrow.EndPoint.X),
                Math.Min(arrow.StartPoint.Y, arrow.EndPoint.Y),
                Math.Abs(arrow.EndPoint.X - arrow.StartPoint.X),
                Math.Abs(arrow.EndPoint.Y - arrow.StartPoint.Y));
        }
        else if (annotation is LineAnnotation line)
        {
            // Similar for lines
            if (handle == ResizeHandle.TopLeft || handle == ResizeHandle.Left || handle == ResizeHandle.BottomLeft)
            {
                line.StartPoint = new System.Drawing.Point(newBounds.Left, newBounds.Top);
            }
            else if (handle == ResizeHandle.TopRight || handle == ResizeHandle.Right || handle == ResizeHandle.BottomRight)
            {
                line.EndPoint = new System.Drawing.Point(newBounds.Right, newBounds.Bottom);
            }
            else if (handle == ResizeHandle.Top)
            {
                line.StartPoint = new System.Drawing.Point(line.StartPoint.X, newBounds.Top);
            }
            else if (handle == ResizeHandle.Bottom)
            {
                line.EndPoint = new System.Drawing.Point(line.EndPoint.X, newBounds.Bottom);
            }
            // Update bounds after changing points
            line.Bounds = new System.Drawing.Rectangle(
                Math.Min(line.StartPoint.X, line.EndPoint.X),
                Math.Min(line.StartPoint.Y, line.EndPoint.Y),
                Math.Abs(line.EndPoint.X - line.StartPoint.X),
                Math.Abs(line.EndPoint.Y - line.StartPoint.Y));
        }
        else
        {
            // For rectangles and text, resize bounds directly
            annotation.Bounds = newBounds;
        }
    }
}

enum AnnotationType
{
    Selector,
    Hand,
    Arrow,
    Rectangle,
    Line,
    Text
}

enum ResizeHandle
{
    None,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left
}

