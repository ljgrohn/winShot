using System.Runtime.InteropServices;

namespace SnapMark.Capture;

public static class DpiHelper
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    private const int LOGPIXELSX = 88;
    private const int LOGPIXELSY = 90;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    private const int MDT_EFFECTIVE_DPI = 0;

    public static uint GetDpiForWindowHandle(IntPtr hwnd)
    {
        try
        {
            return GetDpiForWindow(hwnd);
        }
        catch
        {
            // Fallback to system DPI
            return GetSystemDpi();
        }
    }

    public static uint GetSystemDpi()
    {
        IntPtr hdc = GetDC(IntPtr.Zero);
        if (hdc != IntPtr.Zero)
        {
            int dpi = GetDeviceCaps(hdc, LOGPIXELSX);
            ReleaseDC(IntPtr.Zero, hdc);
            return (uint)dpi;
        }
        return 96; // Default DPI
    }

    public static double GetDpiScale(IntPtr hwnd)
    {
        uint dpi = GetDpiForWindowHandle(hwnd);
        return dpi / 96.0;
    }

    public static int ScaleToDpi(int value, double dpiScale)
    {
        return (int)(value * dpiScale);
    }

    public static int ScaleFromDpi(int value, double dpiScale)
    {
        return (int)(value / dpiScale);
    }
}


