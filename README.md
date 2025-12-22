# SnapMark

A Windows-native screenshot and annotation tool built with WinUI 3.

## Prerequisites

### Windows App SDK Runtime

**IMPORTANT**: SnapMark requires the Windows App SDK Runtime to be installed on your system.

1. Download the Windows App SDK Runtime from: https://aka.ms/windowsappsdk/1.5/stable/windowsappruntimeinstall-x64.exe
2. Run the installer
3. Restart your computer if prompted

Alternatively, you can install it via winget:
```powershell
winget install Microsoft.WindowsAppRuntime
```

## Building

```bash
dotnet build SnapMark.sln
```

## Running

After installing the Windows App SDK Runtime:

```bash
dotnet run --project SnapMark.UI/SnapMark.UI.csproj
```

Or run the built executable directly:
```bash
.\SnapMark.UI\bin\Debug\net8.0-windows10.0.19041.0\SnapMark.UI.exe
```

## Features

- **Screenshot Capture**: Region, full screen, and active window capture
- **Annotation Tools**: Arrow, rectangle, line, and text annotations
- **Export**: Copy to clipboard or save to file
- **Keyboard Shortcuts**: 
  - Win+Shift+S - Region capture
  - Win+Shift+F - Full screen capture
  - Win+Shift+W - Active window capture

## Development

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup instructions.


