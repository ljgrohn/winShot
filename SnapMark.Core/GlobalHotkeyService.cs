using System.Runtime.InteropServices;
using System.Windows.Input;
using System;

namespace SnapMark.Core;

public class GlobalHotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private readonly Dictionary<int, Action> _hotkeyCallbacks = new();
    private readonly IntPtr _hwnd;
    private bool _disposed = false;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private LowLevelKeyboardProc? _proc;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private IntPtr _hookId = IntPtr.Zero;

    public event Action? RegionCaptureRequested;
    public event Action? FullScreenCaptureRequested;
    public event Action? ActiveWindowCaptureRequested;

    public GlobalHotkeyService(IntPtr windowHandle)
    {
        _hwnd = windowHandle;
        InitializeHotkeys();
    }

    private void InitializeHotkeys()
    {
        // For WinUI 3, we'll use a different approach with low-level keyboard hook
        // This is a simplified implementation - in production, use proper Win32 message handling
        _proc = HookCallback;
        _hookId = SetHook(_proc);
    }

    private IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        try
        {
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            if (curModule != null)
            {
                var handle = GetModuleHandle(curModule.ModuleName);
                if (handle == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("GetModuleHandle returned zero");
                    return IntPtr.Zero;
                }
                var hookId = SetWindowsHookEx(WH_KEYBOARD_LL, proc, handle, 0);
                if (hookId == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"SetWindowsHookEx failed with error: {Marshal.GetLastWin32Error()}");
                }
                return hookId;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SetHook error: {ex.Message}");
        }
        return IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            
            // Check for Win+Shift+S (Region Capture)
            if (IsKeyPressed(0x5B) && IsKeyPressed(0x10) && vkCode == 0x53) // Win+Shift+S
            {
                RegionCaptureRequested?.Invoke();
                return (IntPtr)1; // Suppress key
            }
            
            // Check for Win+Shift+F (Full Screen)
            if (IsKeyPressed(0x5B) && IsKeyPressed(0x10) && vkCode == 0x46) // Win+Shift+F
            {
                FullScreenCaptureRequested?.Invoke();
                return (IntPtr)1; // Suppress key
            }
            
            // Check for Win+Shift+W (Active Window)
            if (IsKeyPressed(0x5B) && IsKeyPressed(0x10) && vkCode == 0x57) // Win+Shift+W
            {
                ActiveWindowCaptureRequested?.Invoke();
                return (IntPtr)1; // Suppress key
            }
        }
        
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private static bool IsKeyPressed(int vKey)
    {
        return (GetAsyncKeyState(vKey) & 0x8000) != 0;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
            }
            _disposed = true;
        }
    }
}

