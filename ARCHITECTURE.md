# SnapMark Architecture

## Overview

SnapMark is a Windows-native screenshot and annotation tool built with WinUI 3 and .NET 8. The application follows a modular architecture with clear separation of concerns.

## Solution Structure

- **SnapMark.Core**: Core business logic, settings, and shared utilities
- **SnapMark.Capture**: Screenshot capture engine with DPI awareness
- **SnapMark.Editor**: Annotation editor components and tools
- **SnapMark.UI**: WinUI 3 application shell and main window

## Key Components

### Settings Service
- Location: `SnapMark.Core/SettingsService.cs`
- Stores settings as JSON in `%AppData%/SnapMark/settings.json`
- Manages hotkey configuration, default paths, and annotation styles

### Global Hotkey Service
- Location: `SnapMark.Core/GlobalHotkeyService.cs`
- Uses low-level keyboard hooks to capture global hotkeys
- Supports Win+Shift+S (region), Win+Shift+F (fullscreen), Win+Shift+W (window)

### Tray Icon Service
- Location: `SnapMark.Core/TrayIconService.cs`
- Manages system tray icon and context menu
- Uses Windows Forms NotifyIcon for compatibility

### Single Instance Manager
- Location: `SnapMark.Core/SingleInstanceManager.cs`
- Ensures only one instance runs at a time using Mutex

## Data Flow

1. **Application Startup**
   - App initializes services (Settings, Tray Icon, Hotkeys)
   - Registers global hotkeys
   - Shows tray icon

2. **Capture Flow**
   - Hotkey pressed → GlobalHotkeyService fires event
   - Capture engine captures screen/window/region
   - Bitmap passed to Editor
   - Editor window opens with captured image

3. **Annotation Flow**
   - User selects tool from toolbar
   - Draws annotation on canvas
   - Annotations stored in collection
   - Undo/redo system tracks changes

4. **Export Flow**
   - User presses Enter or Ctrl+S
   - Annotations rendered onto bitmap
   - Image copied to clipboard or saved to file

## DPI Awareness

- Per-Monitor DPI Awareness v2 enabled in app.manifest
- All capture operations use DPI-aware APIs
- Coordinate transformations handled by DpiHelper utility

## Performance Considerations

- Capture operations target < 100ms hotkey → crosshair
- Editor rendering uses hardware acceleration where possible
- Bitmap operations optimized for memory efficiency


