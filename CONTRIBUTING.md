# Contributing to SnapMark

## Development Setup

### Prerequisites
- Windows 10 (22H2+) or Windows 11
- .NET 8.0 SDK or later
- Visual Studio 2022 or Visual Studio Code with C# extension
- Windows App SDK (installed via NuGet)

### Building the Solution

```bash
dotnet restore SnapMark.sln
dotnet build SnapMark.sln
```

### Running the Application

```bash
dotnet run --project SnapMark.UI/SnapMark.UI.csproj
```

## Project Structure

- `SnapMark.Core/`: Core business logic and services
- `SnapMark.Capture/`: Screenshot capture engine
- `SnapMark.Editor/`: Annotation editor components
- `SnapMark.UI/`: WinUI 3 application shell

## Code Style

- Follow C# coding conventions
- Use nullable reference types
- Document public APIs with XML comments
- Keep methods focused and small

## Testing

- Unit tests should be added for core functionality
- Manual testing required for UI components
- Performance testing for capture operations

## Submitting Changes

1. Create a feature branch
2. Make your changes
3. Ensure the solution builds without errors
4. Test your changes
5. Submit a pull request


