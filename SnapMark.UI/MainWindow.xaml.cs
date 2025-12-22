using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SnapMark.Capture;
using System;

namespace SnapMark.UI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "SnapMark";
        
        // Ensure window is properly sized
        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(500, 400));
    }

    private void TestCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Capturing full screen...";
            var captureService = new ScreenCaptureService();
            var result = captureService.CaptureFullScreen();
            
            var editorWindow = new EditorWindow();
            editorWindow.LoadCapture(result);
            editorWindow.Activate();
            
            StatusText.Text = "Capture successful! Editor window opened.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void QuitButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        app.Quit();
    }
}

