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
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;

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
    private System.Drawing.Rectangle _moveStartBounds;
    
    // Selection rectangle state (for drag-to-select)
    private bool _isSelecting = false;
    private Windows.Foundation.Point? _selectionStartPoint;
    private Microsoft.UI.Xaml.Shapes.Rectangle? _selectionRectangle;
    
    // Resize state
    private bool _isResizing = false;
    private ResizeHandle? _resizeHandle;
    private System.Drawing.Rectangle _resizeStartBounds;
    
    // Zoom state - prevent auto-centering when user zooms manually
    private bool _isInitialFit = true;
    
    // Available fonts list
    private static readonly string[] AvailableFonts = new[]
    {
        "Arial",
        "Calibri",
        "Comic Sans MS",
        "Courier New",
        "Georgia",
        "Impact",
        "Times New Roman",
        "Trebuchet MS",
        "Verdana",
        "Segoe UI"
    };

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
        
        // Populate font combo box
        InitializeFontComboBox();
    }
    
    private void InitializeFontComboBox()
    {
        FontComboBox.Items.Clear();
        foreach (var fontName in AvailableFonts)
        {
            FontComboBox.Items.Add(fontName);
        }
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

    private async void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        await CopyToClipboard();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveToFile();
    }

    private async Task CopyToClipboard()
    {
        try
        {
            var bitmap = RenderAnnotations();
            
            // Create DataPackage with multiple formats for better compatibility
            var dataPackage = new DataPackage();
            
            // Method 1: Set as bitmap (for modern apps)
            var stream = new System.IO.MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            var ras = stream.AsRandomAccessStream();
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(ras));
            
            // Method 2: Also set as storage items for better compatibility
            // Create a temporary file and add it to the clipboard
            var tempFile = await Windows.Storage.ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                $"SnapMark_{Guid.NewGuid()}.png", 
                Windows.Storage.CreationCollisionOption.ReplaceExisting);
            
            using (var fileStream = await tempFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            {
                stream.Position = 0;
                var writeStream = fileStream.AsStreamForWrite();
                await stream.CopyToAsync(writeStream);
                await writeStream.FlushAsync();
            }
            
            // Set the file in the clipboard
            var storageItems = new[] { tempFile };
            dataPackage.SetStorageItems(storageItems);
            
            // Set clipboard content
            Clipboard.SetContent(dataPackage);
            
            // Request the clipboard to be copied (this ensures it's committed)
            Clipboard.Flush();
            
            // Clean up temp file after a delay (give clipboard time to read it)
            _ = Task.Delay(5000).ContinueWith(async _ =>
            {
                try
                {
                    await tempFile.DeleteAsync();
                }
                catch { /* Ignore cleanup errors */ }
            });
        }
        catch (Exception ex)
        {
            // Log error in production
            System.Diagnostics.Debug.WriteLine($"Clipboard copy failed: {ex.Message}");
        }
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

        // Update text bounds before rendering to ensure they match actual text size
        foreach (var annotation in _annotations)
        {
            if (annotation is TextAnnotation textAnnotation)
            {
                UpdateTextBounds(textAnnotation);
            }
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
            _ = CopyToClipboard();
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
        // Note: Cursor management in WinUI 3 requires ProtectedCursor which is only accessible
        // from classes inheriting from UIElement. Since we can't access it here, cursor
        // will use default behavior. For full cursor control, consider creating a custom
        // ScrollViewer control that inherits from ScrollViewer.
        if (_currentTool == AnnotationType.Hand)
        {
            // Cursor will be handled by the framework's default behavior
        }
    }
    
    private void ImageScrollViewer_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        // Reset cursor when leaving ScrollViewer
        // Note: See comment in PointerEntered handler
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

    private void FontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_annotations.SelectedAnnotation is not TextAnnotation textAnnotation) return;
        if (FontComboBox.SelectedIndex < 0 || FontComboBox.SelectedIndex >= AvailableFonts.Length) return;
        
        var selectedFontName = AvailableFonts[FontComboBox.SelectedIndex];
        var newFontFamily = new System.Drawing.FontFamily(selectedFontName);
        
        // Update annotation font via command
        var command = new ChangeFontCommand(textAnnotation, newFontFamily);
        _commandManager.ExecuteCommand(command);
        
        // Update bounds to match new font
        UpdateTextBounds(textAnnotation);
        
        RedrawAnnotations();
    }

    private void FontSizeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_annotations.SelectedAnnotation is not TextAnnotation textAnnotation) return;
        
        var newSize = (float)e.NewValue;
        FontSizeText.Text = ((int)newSize).ToString();
        
        // Update annotation font size via command
        var command = new ChangeFontSizeCommand(textAnnotation, newSize);
        _commandManager.ExecuteCommand(command);
        
        // Update bounds to match new font size
        UpdateTextBounds(textAnnotation);
        
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
            
            // Show/hide controls based on annotation type
            bool isTextAnnotation = _annotations.SelectedAnnotation is TextAnnotation;
            
            if (isTextAnnotation)
            {
                // Hide stroke width controls for text
                StrokeWidthLabel.Visibility = Visibility.Collapsed;
                StrokeWidthSlider.Visibility = Visibility.Collapsed;
                StrokeWidthText.Visibility = Visibility.Collapsed;
                
                // Show font controls for text
                FontLabel.Visibility = Visibility.Visible;
                FontComboBox.Visibility = Visibility.Visible;
                FontSizeLabel.Visibility = Visibility.Visible;
                FontSizeSlider.Visibility = Visibility.Visible;
                FontSizeText.Visibility = Visibility.Visible;
                
                var textAnnotation = (TextAnnotation)_annotations.SelectedAnnotation;
                FontSizeSlider.Value = textAnnotation.Font.Size;
                FontSizeText.Text = ((int)textAnnotation.Font.Size).ToString();
                
                // Set selected font in combo box
                var fontFamilyName = textAnnotation.Font.FontFamily.Name;
                var index = Array.IndexOf(AvailableFonts, fontFamilyName);
                if (index >= 0)
                {
                    FontComboBox.SelectedIndex = index;
                }
                else
                {
                    // If font not in list, select first item (Arial)
                    FontComboBox.SelectedIndex = 0;
                }
            }
            else
            {
                // Show stroke width controls for shapes
                StrokeWidthLabel.Visibility = Visibility.Visible;
                StrokeWidthSlider.Visibility = Visibility.Visible;
                StrokeWidthText.Visibility = Visibility.Visible;
                StrokeWidthSlider.Value = _annotations.SelectedAnnotation.StrokeWidth;
                StrokeWidthText.Text = _annotations.SelectedAnnotation.StrokeWidth.ToString();
                
                // Hide font controls for shapes
                FontLabel.Visibility = Visibility.Collapsed;
                FontComboBox.Visibility = Visibility.Collapsed;
                FontSizeLabel.Visibility = Visibility.Collapsed;
                FontSizeSlider.Visibility = Visibility.Collapsed;
                FontSizeText.Visibility = Visibility.Collapsed;
            }
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
                // Text annotations should not be manually resized - they auto-size to fit text
                // Only allow resizing for non-text annotations
                if (hitAnnotation is not TextAnnotation)
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
                        e.Handled = true;
                        return;
                    }
                }
                
                // Select annotation and prepare for potential move (but don't start moving yet)
                _moveStartPoint = point;
                _moveStartAnnotation = hitAnnotation;
                _moveStartBounds = hitAnnotation.Bounds;
                _annotations.SelectAnnotation(hitAnnotation);
                UpdatePropertyPanel();
                AnnotationCanvas.CapturePointer(e.Pointer);
                RedrawAnnotations();
                e.Handled = true;
                return;
            }
            
            // Start selection rectangle drag on empty space
            _isSelecting = true;
            _selectionStartPoint = point;
            AnnotationCanvas.CapturePointer(e.Pointer);
            e.Handled = true;
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
        
        // Handle selection rectangle drawing
        if (_isSelecting && _selectionStartPoint.HasValue)
        {
            var startX = Math.Min(_selectionStartPoint.Value.X, currentPoint.X);
            var startY = Math.Min(_selectionStartPoint.Value.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _selectionStartPoint.Value.X);
            var height = Math.Abs(currentPoint.Y - _selectionStartPoint.Value.Y);
            
            // Remove previous selection rectangle if exists
            if (_selectionRectangle != null)
            {
                AnnotationCanvas.Children.Remove(_selectionRectangle);
            }
            
            // Create new selection rectangle preview
            _selectionRectangle = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = width,
                Height = height,
                Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Blue),
                StrokeThickness = 2,
                StrokeDashArray = new Microsoft.UI.Xaml.Media.DoubleCollection { 4, 4 },
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(30, 0, 120, 215)) // Semi-transparent blue fill
            };
            Canvas.SetLeft(_selectionRectangle, startX);
            Canvas.SetTop(_selectionRectangle, startY);
            AnnotationCanvas.Children.Add(_selectionRectangle);
            
            e.Handled = true;
            return;
        }
        
        // Handle moving - only start moving if pointer has moved significantly
        if (_moveStartPoint.HasValue && _moveStartAnnotation != null)
        {
            var deltaX = currentPoint.X - _moveStartPoint.Value.X;
            var deltaY = currentPoint.Y - _moveStartPoint.Value.Y;
            
            // Only start moving if pointer has moved more than a small threshold
            const double moveThreshold = 3.0;
            if (Math.Abs(deltaX) > moveThreshold || Math.Abs(deltaY) > moveThreshold)
            {
                if (!_isMoving)
                {
                    _isMoving = true;
                }
                
                // Convert delta to canvas coordinates accounting for zoom
                var zoomFactor = ImageScrollViewer.ZoomFactor;
                var drawingDelta = new System.Drawing.Point((int)(deltaX / zoomFactor), (int)(deltaY / zoomFactor));
                
                // Restore original bounds, then apply new delta
                _moveStartAnnotation.Bounds = _moveStartBounds;
                _moveStartAnnotation.Move(drawingDelta);
                RedrawAnnotations();
                e.Handled = true;
                return;
            }
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
        
        // Handle selection rectangle end
        if (_isSelecting && _selectionStartPoint.HasValue)
        {
            var endPoint = e.GetCurrentPoint(AnnotationCanvas).Position;
            
            // Check if this was just a click (very small movement)
            var deltaX = Math.Abs(endPoint.X - _selectionStartPoint.Value.X);
            var deltaY = Math.Abs(endPoint.Y - _selectionStartPoint.Value.Y);
            const double clickThreshold = 3.0;
            
            if (deltaX < clickThreshold && deltaY < clickThreshold)
            {
                // Just a click - clear selection
                _annotations.ClearSelection();
                UpdatePropertyPanel();
            }
            else
            {
                // Convert selection rectangle to drawing coordinates
                var zoomFactor = ImageScrollViewer.ZoomFactor;
                var startDrawingPoint = WinUIToDrawingPoint(_selectionStartPoint.Value);
                var endDrawingPoint = WinUIToDrawingPoint(endPoint);
                
                var selectionRect = new System.Drawing.Rectangle(
                    Math.Min(startDrawingPoint.X, endDrawingPoint.X),
                    Math.Min(startDrawingPoint.Y, endDrawingPoint.Y),
                    Math.Abs(endDrawingPoint.X - startDrawingPoint.X),
                    Math.Abs(endDrawingPoint.Y - startDrawingPoint.Y));
                
                // Find all annotations within the selection rectangle
                var selectedAnnotations = _annotations.HitTest(selectionRect);
                
                // Select the first annotation found (or clear selection if none)
                if (selectedAnnotations.Count > 0)
                {
                    // Select the topmost annotation (first in list since HitTest returns from highest Z-order)
                    _annotations.SelectAnnotation(selectedAnnotations[0]);
                    UpdatePropertyPanel();
                }
                else
                {
                    _annotations.ClearSelection();
                    UpdatePropertyPanel();
                }
            }
            
            // Clean up selection rectangle preview
            if (_selectionRectangle != null)
            {
                AnnotationCanvas.Children.Remove(_selectionRectangle);
                _selectionRectangle = null;
            }
            
            _isSelecting = false;
            _selectionStartPoint = null;
            AnnotationCanvas.ReleasePointerCapture(e.Pointer);
            RedrawAnnotations();
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
        
        // Handle move end - check if we were actually moving or just selecting
        if (_moveStartPoint.HasValue && _moveStartAnnotation != null)
        {
            if (_isMoving)
            {
                // We were actually moving, so commit the move
                var endPoint = e.GetCurrentPoint(AnnotationCanvas).Position;
                var deltaX = endPoint.X - _moveStartPoint.Value.X;
                var deltaY = endPoint.Y - _moveStartPoint.Value.Y;
                
                // Convert delta to canvas coordinates accounting for zoom
                var zoomFactor = ImageScrollViewer.ZoomFactor;
                var drawingDelta = new System.Drawing.Point((int)(deltaX / zoomFactor), (int)(deltaY / zoomFactor));
                
                // Restore original position before applying command
                _moveStartAnnotation.Bounds = _moveStartBounds;
                
                if (drawingDelta.X != 0 || drawingDelta.Y != 0)
                {
                    var command = new MoveAnnotationCommand(_moveStartAnnotation, drawingDelta);
                    _commandManager.ExecuteCommand(command);
                }
            }
            else
            {
                // Just a click selection, restore bounds in case they were modified during preview
                _moveStartAnnotation.Bounds = _moveStartBounds;
            }
            
            _isMoving = false;
            _moveStartPoint = null;
            _moveStartAnnotation = null;
            AnnotationCanvas.ReleasePointerCapture(e.Pointer);
            RedrawAnnotations();
            e.Handled = true;
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
            XamlRoot = this.Content.XamlRoot,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x1a, 0x1a, 0x1a))
        };
        
        var container = new StackPanel { Spacing = 10, MinWidth = 350 };
        
        var textBox = new TextBox
        {
            PlaceholderText = "Enter annotation text... (Press Enter for new line)",
            AcceptsReturn = true,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            MinHeight = 60,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x1a, 0x1a, 0x1a)),
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x40, 0x40, 0x40))
        };
        
        var fontContainer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var fontLabel = new TextBlock 
        { 
            Text = "Font:", 
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
            Width = 60
        };
        var fontComboBox = new ComboBox 
        { 
            Width = 150,
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var fontName in AvailableFonts)
        {
            fontComboBox.Items.Add(fontName);
        }
        fontComboBox.SelectedIndex = 0; // Default to Arial
        
        fontContainer.Children.Add(fontLabel);
        fontContainer.Children.Add(fontComboBox);
        
        var fontSizeContainer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var fontSizeLabel = new TextBlock 
        { 
            Text = "Font Size:", 
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
            Width = 80
        };
        var fontSizeSlider = new Slider 
        { 
            Minimum = 8, 
            Maximum = 72, 
            Value = 12, 
            Width = 150,
            VerticalAlignment = VerticalAlignment.Center
        };
        var fontSizeText = new TextBlock 
        { 
            Text = "12", 
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
            Width = 30
        };
        
        fontSizeSlider.ValueChanged += (s, e) => 
        {
            fontSizeText.Text = ((int)e.NewValue).ToString();
        };
        
        fontSizeContainer.Children.Add(fontSizeLabel);
        fontSizeContainer.Children.Add(fontSizeSlider);
        fontSizeContainer.Children.Add(fontSizeText);
        
        container.Children.Add(textBox);
        container.Children.Add(fontContainer);
        container.Children.Add(fontSizeContainer);
        
        dialog.Content = container;
        
        // Focus the text box when dialog opens
        dialog.Opened += (s, e) => textBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        
        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            var drawingPoint = WinUIToDrawingPoint(position);
            var fontSize = (float)fontSizeSlider.Value;
            var selectedFontName = fontComboBox.SelectedIndex >= 0 && fontComboBox.SelectedIndex < AvailableFonts.Length
                ? AvailableFonts[fontComboBox.SelectedIndex]
                : "Arial";
            var fontFamily = new System.Drawing.FontFamily(selectedFontName);
            
            var textAnnotation = new TextAnnotation(drawingPoint, textBox.Text);
            // Update font family and size
            var oldFont = textAnnotation.Font;
            textAnnotation.Font = new Font(fontFamily, fontSize, oldFont.Style);
            oldFont.Dispose();
            
            // Update bounds to match actual text size
            UpdateTextBounds(textAnnotation);
            
            var command = new CreateAnnotationCommand(_annotations, textAnnotation);
            _commandManager.ExecuteCommand(command);
            RedrawAnnotations();
        }
    }

    private void RedrawAnnotations()
    {
        // Update text bounds before rendering to ensure they match actual text size
        foreach (var annotation in _annotations)
        {
            if (annotation is TextAnnotation textAnnotation)
            {
                UpdateTextBounds(textAnnotation);
            }
        }
        
        // Clear existing annotation shapes, borders (for text), and handles (but keep preview shape)
        var elementsToRemove = AnnotationCanvas.Children
            .Where(child => 
                (child is Microsoft.UI.Xaml.Shapes.Shape shape && shape != _previewShape) ||
                child is Border)
            .ToList();
        
        foreach (var element in elementsToRemove)
        {
            AnnotationCanvas.Children.Remove(element);
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
        
        // Draw selection rectangle - account for zoom level
        var zoomFactor = ImageScrollViewer.ZoomFactor;
        var topLeftWinUI = DrawingToWinUIPoint(new System.Drawing.Point(bounds.Left, bounds.Top));
        
        // Scale width and height by zoom factor to match the zoomed annotation
        var selectionRect = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = bounds.Width * zoomFactor,
            Height = bounds.Height * zoomFactor,
            Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Blue),
            StrokeThickness = 2,
            StrokeDashArray = new Microsoft.UI.Xaml.Media.DoubleCollection { 4, 4 },
            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };
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
        var textColor = text.Color;
        var isBlack = textColor.R == 0 && textColor.G == 0 && textColor.B == 0;
        
        // Set background: white for black text, #1a1a1a for other colors
        var backgroundColor = isBlack 
            ? Microsoft.UI.Colors.White 
            : Windows.UI.Color.FromArgb(255, 0x1a, 0x1a, 0x1a);
        
        // Measure actual text size using Graphics (same method as UpdateBoundsFromText)
        // This ensures padding is calculated from actual text size, not bounds
        // Handle multi-line text
        using var tempBitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(tempBitmap);
        var stringFormat = new System.Drawing.StringFormat(System.Drawing.StringFormatFlags.LineLimit);
        var textSize = graphics.MeasureString(text.Text, text.Font, int.MaxValue, stringFormat);
        stringFormat.Dispose();
        var textWidth = textSize.Width;
        var textHeight = textSize.Height;
        
        // Calculate padding: 2-character buffer on left/right, 3% top/bottom
        var twoCharSize = graphics.MeasureString("MM", text.Font);
        var horizontalPadding = (int)twoCharSize.Width;
        var topPadding = (int)(textHeight * 0.03);
        var bottomPadding = topPadding; // Use same padding for bottom
        
        // Create TextBlock with center alignment (supports multi-line)
        // TextBlock will automatically fill the Border's content area (Border width - padding)
        var textBlock = new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Text = text.Text,
            Foreground = ColorToBrush(text.Color),
            FontSize = text.Font.Size,
            TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
        };
        
        // Wrap TextBlock in Border with rounded corners and padding
        // Border size should match the bounds (which already includes padding from UpdateBoundsFromText)
        var border = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(backgroundColor),
            Padding = new Thickness(horizontalPadding, topPadding, horizontalPadding, bottomPadding),
            CornerRadius = new CornerRadius(4), // Slightly rounded corners
            Child = textBlock,
            Width = text.Bounds.Width,
            Height = text.Bounds.Height
        };
        
        Canvas.SetLeft(border, text.Bounds.X);
        Canvas.SetTop(border, text.Bounds.Y);
        AnnotationCanvas.Children.Add(border);
        return null; // Border is not a Shape
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

    private void UpdateTextBounds(TextAnnotation textAnnotation)
    {
        // Create a temporary bitmap to get a Graphics object for text measurement
        using var tempBitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(tempBitmap);
        textAnnotation.UpdateBoundsFromText(graphics);
    }

    private void ResizeAnnotation(IAnnotation annotation, ResizeHandle handle, System.Drawing.Point newPoint)
    {
        // Text annotations should not be manually resized - they auto-size to fit text
        if (annotation is TextAnnotation textAnnotation)
        {
            // Recalculate bounds from text instead of manual resize
            UpdateTextBounds(textAnnotation);
            return;
        }
        
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

