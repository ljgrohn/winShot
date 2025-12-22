using System;
using WinRT.Interop;

namespace SnapMark.UI;

internal static class WindowNative
{
    public static IntPtr GetWindowHandle(Microsoft.UI.Xaml.Window window)
    {
        return WinRT.Interop.WindowNative.GetWindowHandle(window);
    }
}

