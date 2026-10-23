using System.Runtime.InteropServices;
using ClipVault.Core.Models;
using Serilog;

namespace ClipVault_App.Services;

public sealed class SafePasteService(ClipboardMonitorService clipboard)
{
    private nint _target;
    public void RememberTarget() { var foreground = GetForegroundWindow(); if (foreground != 0) _target = foreground; }

    public async Task<bool> PasteAsync(ClipboardItem item)
    {
        if (_target == 0 || !IsWindow(_target)) return false;
        await clipboard.CopyAsync(item);
        if (!SetForegroundWindow(_target) || GetForegroundWindow() != _target) return false;
        var inputs = new[] { Key(0x11, false), Key(0x56, false), Key(0x56, true), Key(0x11, true) };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length) { Log.Warning("Paste input was not fully delivered to validated target window"); return false; }
        return true;
    }

    private static INPUT Key(ushort code, bool up) => new() { type = 1, U = new InputUnion { ki = new KEYBDINPUT { wVk = code, dwFlags = up ? 2u : 0u } } };
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public nint dwExtraInfo; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
}
