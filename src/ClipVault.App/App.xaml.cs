using ClipVault.Core.Services;
using ClipVault_App.Services;
using ClipVault_App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Serilog;

namespace ClipVault_App;

public partial class App : Application
{
    private readonly Mutex _singleInstance = new(true, "Local\\ClipVault-2E57EAC0-22A4-47C5-9A5F-48079BD92880");
    public static Window Window { get; private set; } = null!;
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipVault");
        Directory.CreateDirectory(data);
        Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.File(Path.Combine(data, "Logs", "clipvault-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14).CreateLogger();
        Services = new ServiceCollection()
            .AddSingleton(new ClipboardRepository(Path.Combine(data, "clipvault.db")))
            .AddSingleton(new SettingsStore(Path.Combine(data, "settings.json")))
            .AddSingleton<ClipboardMonitorService>()
            .AddSingleton<SafePasteService>()
            .AddSingleton<StartupService>()
            .AddSingleton<BackupService>()
            .AddSingleton<ImageOcrService>()
            .AddTransient<MainPageViewModel>()
            .BuildServiceProvider();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            if (!_singleInstance.WaitOne(TimeSpan.Zero, true)) { Exit(); return; }
            var monitor = Services.GetRequiredService<ClipboardMonitorService>();
            await monitor.InitializeAsync();
            Services.GetRequiredService<StartupService>().Apply(monitor.Settings.StartWithWindows);
            Window = new MainWindow();
            Window.Activate();
            Log.Information("ClipVault started");
        }
        catch (Exception ex) { Log.Fatal(ex, "ClipVault startup failed"); throw; }
    }
}
