using System.Drawing;
using System.Windows.Forms;

namespace SnapMark.Core;

public class TrayIconService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private bool _disposed = false;

    public event EventHandler? SettingsRequested;
    public event EventHandler? QuitRequested;

    public TrayIconService()
    {
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "SnapMark",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();
        
        var settingsItem = new ToolStripMenuItem("Settings");
        settingsItem.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(settingsItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += (s, e) => QuitRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(quitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private Icon CreateDefaultIcon()
    {
        // Create a simple icon programmatically
        // In production, use a proper icon resource
        using var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.FillEllipse(Brushes.Blue, 2, 2, 12, 12);
        graphics.DrawLine(Pens.White, 4, 8, 12, 8);
        graphics.DrawLine(Pens.White, 8, 4, 8, 12);
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void ShowNotification(string title, string message)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _notifyIcon?.Dispose();
            _disposed = true;
        }
    }
}


