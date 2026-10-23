using Microsoft.Win32;

namespace ClipVault_App.Services;

public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClipVault";

    public void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled && Environment.ProcessPath is { Length: > 0 } executable)
            key.SetValue(ValueName, $"\"{executable}\" --background", RegistryValueKind.String);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
