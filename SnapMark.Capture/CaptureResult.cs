using System.Drawing;

namespace SnapMark.Capture;

public class CaptureResult
{
    public Bitmap Bitmap { get; set; } = null!;
    public Rectangle Bounds { get; set; }
    public CaptureMode Mode { get; set; }
    public DateTime CaptureTime { get; set; } = DateTime.Now;
}

public enum CaptureMode
{
    Region,
    FullScreen,
    ActiveWindow
}


