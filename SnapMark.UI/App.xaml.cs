using Microsoft.UI.Xaml;
using SnapMark.Core;
using SnapMark.Capture;
using System;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace SnapMark.UI;

public partial class App : Application
{
    private Window? _mainWindow;
    // private TrayIconService? _trayIconService; // Disabled - requires Windows Forms message pump
    private GlobalHotkeyService? _hotkeyService;
    private SettingsService? _settingsService;
    private SingleInstanceManager? _singleInstanceManager;
    private ScreenCaptureService? _captureService;
    private RegionSelector? _regionSelector;

    public App()
    {
        Console.WriteLine("App constructor called");
        try
        {
            Console.WriteLine("Initializing component...");
            this.InitializeComponent();
            Console.WriteLine("Component initialized");
            
            // Add global exception handler
            this.UnhandledException += App_UnhandledException;
            
            Console.WriteLine("Checking single instance...");
            // Check for single instance
            _singleInstanceManager = new SingleInstanceManager("SnapMark");
            if (!_singleInstanceManager.IsFirstInstance)
            {
                Console.WriteLine("Another instance is running, exiting...");
                // Another instance is running, exit
                Environment.Exit(0);
                return;
            }
            Console.WriteLine("First instance confirmed");

            Console.WriteLine("Initializing services...");
            // Initialize services
            _settingsService = new SettingsService();
            Console.WriteLine("Settings service initialized");
            
            _captureService = new ScreenCaptureService();
            Console.WriteLine("Capture service initialized");
            
            Console.WriteLine("App constructor completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"App initialization error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"App initialization error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw; // Re-throw to see the error
        }
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Unhandled exception: {e.Message}");
        System.Diagnostics.Debug.WriteLine($"Exception: {e.Exception}");
        System.Diagnostics.Debug.WriteLine($"Stack trace: {e.Exception?.StackTrace}");
        
        // Write to console for visibility
        Console.WriteLine($"CRITICAL ERROR: {e.Exception?.Message}");
        Console.WriteLine($"Stack trace: {e.Exception?.StackTrace}");
        
        e.Handled = false; // Let it crash so we can see the error
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Console.WriteLine("OnLaunched called");
        try
        {
            Console.WriteLine("Creating MainWindow...");
            _mainWindow = new MainWindow();
            Console.WriteLine("MainWindow created");
            
            // Get window handle for hotkey service
            try
            {
                Console.WriteLine("Getting window handle...");
                var windowHandle = WindowNative.GetWindowHandle(_mainWindow);
                Console.WriteLine($"Window handle: {windowHandle}");
                
                Console.WriteLine("Initializing hotkey service...");
                _hotkeyService = new GlobalHotkeyService(windowHandle);
                _hotkeyService.RegionCaptureRequested += OnRegionCaptureRequested;
                _hotkeyService.FullScreenCaptureRequested += OnFullScreenCaptureRequested;
                _hotkeyService.ActiveWindowCaptureRequested += OnActiveWindowCaptureRequested;
                Console.WriteLine("Hotkey service initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hotkey service initialization error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"Hotkey service initialization error: {ex.Message}");
                // Continue without hotkeys for now
            }

            // Show and activate the main window
            Console.WriteLine("Activating main window...");
            _mainWindow.Activate();
            Console.WriteLine("Main window activated - app should be visible now");

            // _trayIconService?.ShowNotification("SnapMark", "SnapMark is running in the system tray");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OnLaunched error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"OnLaunched error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw; // Re-throw to see the error
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        // TODO: Open settings window
        _mainWindow?.Activate();
    }

    private void OnQuitRequested(object? sender, EventArgs e)
    {
        _hotkeyService?.Dispose();
        // _trayIconService?.Dispose();
        _regionSelector?.Dispose();
        _singleInstanceManager?.Dispose();
        Exit();
    }

    public void Quit()
    {
        OnQuitRequested(null, EventArgs.Empty);
    }

    private void OnRegionCaptureRequested()
    {
        StartRegionCapture();
    }

    public void StartRegionCapture()
    {
        try
        {
            _regionSelector?.Dispose(); // Dispose previous if exists
            _regionSelector = new RegionSelector();
            _regionSelector.RegionSelected += OnRegionSelected;
            _regionSelector.StartSelection();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Capture error: {ex.Message}");
        }
    }

    private void OnRegionSelected(object? sender, System.Drawing.Rectangle region)
    {
        try
        {
            var result = _captureService?.CaptureRegion(region);
            if (result != null)
            {
                OpenEditor(result);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Capture error: {ex.Message}");
        }
    }

    private void OnFullScreenCaptureRequested()
    {
        try
        {
            var result = _captureService?.CaptureFullScreen();
            if (result != null)
            {
                OpenEditor(result);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Capture error: {ex.Message}");
        }
    }

    private void OnActiveWindowCaptureRequested()
    {
        try
        {
            var result = _captureService?.CaptureActiveWindow();
            if (result != null)
            {
                OpenEditor(result);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Capture error: {ex.Message}");
        }
    }

    private void OpenEditor(Capture.CaptureResult result)
    {
        var editorWindow = new EditorWindow();
        editorWindow.LoadCapture(result);
        editorWindow.Activate();
    }
}


