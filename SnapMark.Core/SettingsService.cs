using System.Text.Json;

namespace SnapMark.Core;

public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SnapMark",
        "settings.json");

    private AppSettings _settings = null!;

    public SettingsService()
    {
        LoadSettings();
    }

    public AppSettings Settings => _settings;

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? GetDefaultSettings();
            }
            else
            {
                _settings = GetDefaultSettings();
                SaveSettings();
            }
        }
        catch
        {
            _settings = GetDefaultSettings();
        }
    }

    public void SaveSettings()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Log error in production
        }
    }

    private static AppSettings GetDefaultSettings()
    {
        return new AppSettings
        {
            Hotkeys = new HotkeySettings
            {
                RegionCapture = new Hotkey { Modifiers = "Win+Shift", Key = "S" },
                FullScreenCapture = new Hotkey { Modifiers = "Win+Shift", Key = "F" },
                ActiveWindowCapture = new Hotkey { Modifiers = "Win+Shift", Key = "W" }
            },
            DefaultSaveLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SnapMark"),
            FilenameTemplate = "Screenshot {date} {time}",
            DefaultAnnotationStyles = new AnnotationStyles
            {
                ArrowColor = "#FF0000",
                RectangleColor = "#0000FF",
                LineColor = "#00FF00",
                TextColor = "#000000",
                StrokeWidth = 2
            },
            AutoCopyOnCapture = false,
            AutoSaveOnCapture = false,
            IncludeCursor = false,
            ExcludeWindowShadow = true
        };
    }
}

public class AppSettings
{
    public HotkeySettings Hotkeys { get; set; } = new();
    public string DefaultSaveLocation { get; set; } = string.Empty;
    public string FilenameTemplate { get; set; } = string.Empty;
    public AnnotationStyles DefaultAnnotationStyles { get; set; } = new();
    public bool AutoCopyOnCapture { get; set; }
    public bool AutoSaveOnCapture { get; set; }
    public bool IncludeCursor { get; set; }
    public bool ExcludeWindowShadow { get; set; }
}

public class HotkeySettings
{
    public Hotkey RegionCapture { get; set; } = new();
    public Hotkey FullScreenCapture { get; set; } = new();
    public Hotkey ActiveWindowCapture { get; set; } = new();
}

public class Hotkey
{
    public string Modifiers { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}

public class AnnotationStyles
{
    public string ArrowColor { get; set; } = "#FF0000";
    public string RectangleColor { get; set; } = "#0000FF";
    public string LineColor { get; set; } = "#00FF00";
    public string TextColor { get; set; } = "#000000";
    public int StrokeWidth { get; set; } = 2;
}

