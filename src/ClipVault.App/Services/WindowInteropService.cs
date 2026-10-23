using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace ClipVault_App.Services;

public sealed class WindowInteropService : IDisposable
{
    private const int GwlWndProc = -4;
    private const uint WmClipboardUpdate = 0x031D;
    private const uint WmHotKey = 0x0312;
    private const uint WmTrayIcon = 0x8001;
    private static readonly nint HwndTopmost = new(-1);
    private static readonly nint HwndNoTopmost = new(-2);
    private const uint SwpNoMove = 0x0002, SwpNoSize = 0x0001, SwpNoActivate = 0x0010;
    private readonly nint _hwnd;
    private readonly WndProc _newWndProc;
    private nint _oldWndProc;
    private nint _trayIconHandle;
    private bool _trayIconAdded;

    public event EventHandler? ClipboardChanged;
    public event EventHandler<int>? HotKeyPressed;
    public event EventHandler<int>? TrayCommand;

    public WindowInteropService(Window window)
    {
        _hwnd = WindowNative.GetWindowHandle(window);
        _newWndProc = WindowProc;
        _oldWndProc = SetWindowLongPtr(_hwnd, GwlWndProc, Marshal.GetFunctionPointerForDelegate(_newWndProc));
    }

    public bool AddClipboardListener() => AddClipboardFormatListener(_hwnd);
    public void RemoveClipboardListener() => RemoveClipboardFormatListener(_hwnd);
    public bool RegisterHotKey(int id, uint modifiers, uint key) => NativeRegisterHotKey(_hwnd, id, modifiers, key);
    public void UnregisterHotKey(int id) => NativeUnregisterHotKey(_hwnd, id);
    public void SetTopmost(bool enabled) => SetWindowPos(_hwnd, enabled ? HwndTopmost : HwndNoTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    public bool AddTrayIcon(string tooltip, string iconPath)
    {
        _trayIconHandle = LoadImage(0, iconPath, 1, 32, 32, 0x10);
        var data = CreateTrayData(tooltip);
        _trayIconAdded = Shell_NotifyIcon(0, ref data);
        return _trayIconAdded;
    }

    public static AppWindow GetAppWindow(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        return AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
    }

    private nint WindowProc(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == WmClipboardUpdate) ClipboardChanged?.Invoke(this, EventArgs.Empty);
        else if (message == WmHotKey) HotKeyPressed?.Invoke(this, wParam.ToInt32());
        else if (message == WmTrayIcon)
        {
            var mouseMessage = lParam.ToInt32() & 0xFFFF;
            if (mouseMessage == 0x0203) TrayCommand?.Invoke(this, 1);
            else if (mouseMessage == 0x0205) ShowTrayMenu();
        }
        return CallWindowProc(_oldWndProc, hwnd, message, wParam, lParam);
    }

    private void ShowTrayMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == 0) return;
        try
        {
            AppendMenu(menu, 0, 1, "Open ClipVault");
            AppendMenu(menu, 0, 2, "Compact Mode");
            AppendMenu(menu, 0x800, 0, null);
            AppendMenu(menu, 0, 3, "Pause / Resume Monitoring");
            AppendMenu(menu, 0, 4, "Toggle Private Mode");
            AppendMenu(menu, 0x800, 0, null);
            AppendMenu(menu, 0, 5, "Exit");
            GetCursorPos(out var point);
            NativeSetForegroundWindow(_hwnd);
            var command = TrackPopupMenu(menu, 0x0100 | 0x0002, point.X, point.Y, 0, _hwnd, 0);
            if (command > 0) TrayCommand?.Invoke(this, command);
        }
        finally { DestroyMenu(menu); }
    }

    private NOTIFYICONDATA CreateTrayData(string tooltip) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = _hwnd, uID = 1, uFlags = 0x1 | 0x2 | 0x4,
        uCallbackMessage = WmTrayIcon, hIcon = _trayIconHandle, szTip = tooltip
    };

    public void Dispose()
    {
        RemoveClipboardFormatListener(_hwnd);
        if (_trayIconAdded) { var data = CreateTrayData(string.Empty); Shell_NotifyIcon(2, ref data); }
        if (_trayIconHandle != 0) DestroyIcon(_trayIconHandle);
        for (var id = 1; id <= 8; id++) NativeUnregisterHotKey(_hwnd, id);
        if (_oldWndProc != 0) SetWindowLongPtr(_hwnd, GwlWndProc, _oldWndProc);
        GC.SuppressFinalize(this);
    }

    private delegate nint WndProc(nint hwnd, uint message, nint wParam, nint lParam);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize; public nint hWnd; public uint uID, uFlags, uCallbackMessage; public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags; public Guid guidItem; public nint hBalloonIcon;
    }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AddClipboardFormatListener(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveClipboardFormatListener(nint hwnd);
    [DllImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool NativeRegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool NativeUnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint LoadImage(nint instance, string name, uint type, int cx, int cy, uint load);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AppendMenu(nint menu, uint flags, nuint id, string? text);
    [DllImport("user32.dll")] private static extern int TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool NativeSetForegroundWindow(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CallWindowProc(nint previous, nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtr64(nint hwnd, int index, nint value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern nint SetWindowLong32(nint hwnd, int index, nint value);
    private static nint SetWindowLongPtr(nint hwnd, int index, nint value) => nint.Size == 8 ? SetWindowLongPtr64(hwnd, index, value) : SetWindowLong32(hwnd, index, value);
}
