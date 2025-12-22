using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace SnapMark.Capture;

public class ScreenCaptureService
{
    private readonly string _metricsLogPath;

    public ScreenCaptureService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SnapMark");
        
        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }

        _metricsLogPath = Path.Combine(appDataPath, "capture_metrics.log");
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    private const int SRCCOPY = 0x00CC0020;
    private const int PW_CLIENTONLY = 0x1;
    private const int PW_RENDERFULLCONTENT = 0x2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public Rectangle ToRectangle()
        {
            return new Rectangle(Left, Top, Right - Left, Bottom - Top);
        }
    }

    public CaptureResult CaptureRegion(Rectangle region)
    {
        var stopwatch = Stopwatch.StartNew();
        var hotkeyTime = DateTime.Now;

        var bitmap = CaptureScreenRegion(region);
        
        stopwatch.Stop();
        LogMetrics("Region", hotkeyTime, stopwatch.ElapsedMilliseconds);

        return new CaptureResult
        {
            Bitmap = bitmap,
            Bounds = region,
            Mode = CaptureMode.Region
        };
    }

    public CaptureResult CaptureFullScreen()
    {
        var stopwatch = Stopwatch.StartNew();
        var hotkeyTime = DateTime.Now;

        // Get virtual screen bounds (all monitors combined)
        var screenBounds = SystemInformation.VirtualScreen;
        var bitmap = CaptureScreenRegion(screenBounds);

        stopwatch.Stop();
        LogMetrics("FullScreen", hotkeyTime, stopwatch.ElapsedMilliseconds);

        return new CaptureResult
        {
            Bitmap = bitmap,
            Bounds = screenBounds,
            Mode = CaptureMode.FullScreen
        };
    }

    public CaptureResult CaptureActiveWindow()
    {
        var stopwatch = Stopwatch.StartNew();
        var hotkeyTime = DateTime.Now;

        IntPtr hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("No active window found");
        }

        GetWindowRect(hWnd, out RECT rect);
        var bounds = rect.ToRectangle();

        var bitmap = CaptureWindow(hWnd, bounds);

        stopwatch.Stop();
        LogMetrics("ActiveWindow", hotkeyTime, stopwatch.ElapsedMilliseconds);

        return new CaptureResult
        {
            Bitmap = bitmap,
            Bounds = bounds,
            Mode = CaptureMode.ActiveWindow
        };
    }

    private Bitmap CaptureScreenRegion(Rectangle region)
    {
        IntPtr hdcScreen = GetDC(IntPtr.Zero);
        IntPtr hdcDest = CreateCompatibleDC(hdcScreen);
        IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, region.Width, region.Height);
        IntPtr hOld = SelectObject(hdcDest, hBitmap);

        BitBlt(hdcDest, 0, 0, region.Width, region.Height, hdcScreen, region.X, region.Y, SRCCOPY);

        Bitmap? bitmap = Image.FromHbitmap(hBitmap);
        if (bitmap == null)
            throw new InvalidOperationException("Failed to create bitmap");

        SelectObject(hdcDest, hOld);
        DeleteObject(hBitmap);
        DeleteDC(hdcDest);
        ReleaseDC(IntPtr.Zero, hdcScreen);

        return bitmap;
    }

    private Bitmap CaptureWindow(IntPtr hWnd, Rectangle bounds)
    {
        // Try PrintWindow with PW_RENDERFULLCONTENT first (works better for modern Windows)
        IntPtr hdcScreen = GetDC(IntPtr.Zero);
        IntPtr hdcMem = CreateCompatibleDC(hdcScreen);
        IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, bounds.Width, bounds.Height);
        IntPtr hOld = SelectObject(hdcMem, hBitmap);

        // Try PW_RENDERFULLCONTENT first (better for modern Windows apps)
        bool success = PrintWindow(hWnd, hdcMem, PW_RENDERFULLCONTENT);
        
        // If PrintWindow fails, fall back to capturing the screen region
        if (!success)
        {
            // Release the memory DC and bitmap
            SelectObject(hdcMem, hOld);
            DeleteObject(hBitmap);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);
            
            // Fall back to screen region capture (more reliable)
            return CaptureScreenRegion(bounds);
        }

        Bitmap? bitmap = Image.FromHbitmap(hBitmap);
        if (bitmap == null)
        {
            // Cleanup and fallback
            SelectObject(hdcMem, hOld);
            DeleteObject(hBitmap);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);
            return CaptureScreenRegion(bounds);
        }

        SelectObject(hdcMem, hOld);
        DeleteObject(hBitmap);
        DeleteDC(hdcMem);
        ReleaseDC(IntPtr.Zero, hdcScreen);

        return bitmap;
    }

    private void LogMetrics(string mode, DateTime hotkeyTime, long elapsedMs)
    {
        try
        {
            var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {mode} | HotkeyTime: {hotkeyTime:HH:mm:ss.fff} | Elapsed: {elapsedMs}ms\n";
            File.AppendAllText(_metricsLogPath, logEntry);
        }
        catch
        {
            // Silently fail if logging fails
        }
    }
}

