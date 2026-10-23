namespace ClipVault.Core.Models;

public sealed record AppSettings
{
    public bool MonitoringEnabled { get; init; } = true;
    public bool PrivateMode { get; init; }
    public bool MainWindowAlwaysOnTop { get; init; }
    public bool CompactWindowAlwaysOnTop { get; init; } = true;
    public bool MinimizeToTray { get; init; } = true;
    public bool CloseToTray { get; init; } = true;
    public bool DuplicateDetection { get; init; } = true;
    public bool StartWithWindows { get; init; }
    public string[] ExcludedApplications { get; init; } = ["1Password.exe", "Bitwarden.exe", "KeePass.exe", "KeePassXC.exe"];
    public int MaximumHistoryItems { get; init; } = 5_000;
    public int RetentionDays { get; init; } = 90;
    public long MaximumItemBytes { get; init; } = 20 * 1024 * 1024;
    public double CompactWindowOpacity { get; init; } = 0.96;
    public string Theme { get; init; } = "Dark";
    public string Accent { get; init; } = "Blue";
    public WindowPlacement MainWindowPlacement { get; init; } = new(80, 80, 1240, 780);
    public WindowPlacement CompactWindowPlacement { get; init; } = new(980, 100, 420, 600);
}

public sealed record WindowPlacement(int X, int Y, int Width, int Height);
