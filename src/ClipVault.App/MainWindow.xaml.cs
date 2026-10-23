using ClipVault_App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Serilog;

namespace ClipVault_App;

public sealed partial class MainWindow : Window
{
    private const uint ModAlt = 0x0001, ModControl = 0x0002, ModShift = 0x0004, ModNoRepeat = 0x4000;
    private readonly ClipboardMonitorService _monitor = App.Services.GetRequiredService<ClipboardMonitorService>();
    private readonly SafePasteService _paste = App.Services.GetRequiredService<SafePasteService>();
    private readonly WindowInteropService _interop;
    private CompactWindow? _compact;
    private bool _isPinned;
    private bool _exitRequested;

    public MainWindow()
    {
        InitializeComponent(); ExtendsContentIntoTitleBar = true; SetTitleBar(AppTitleBar);
        ConfigureCaptionButtons();
        AppTitleBar.ActualThemeChanged += (_, _) => ConfigureCaptionButtons();
        AppWindow.SetIcon("Assets/AppIcon.ico"); AppWindow.Resize(new Windows.Graphics.SizeInt32(1560, 940));
        _interop = new WindowInteropService(this);
        _interop.ClipboardChanged += async (_, _) => await _monitor.CaptureAsync();
        _interop.HotKeyPressed += OnHotKeyPressed;
        _interop.AddClipboardListener(); RegisterHotKeys();
        _isPinned = _monitor.Settings.MainWindowAlwaysOnTop; ApplyPinned();
        ApplyTheme(_monitor.Settings.Theme);
        RootFrame.Navigate(typeof(MainPage));
        ConfigureTrayIcon();
        AppWindow.Closing += AppWindow_Closing;
        AppWindow.Changed += AppWindow_Changed;
        Closed += (_, _) => { _interop.Dispose(); Log.Information("ClipVault stopped"); Log.CloseAndFlush(); };
    }

    private void RegisterHotKeys()
    {
        if (!_interop.RegisterHotKey(1, ModControl | ModShift | ModNoRepeat, 0x56)) Log.Warning("Global shortcut Ctrl+Shift+V is unavailable");
        if (!_interop.RegisterHotKey(2, ModControl | ModShift | ModNoRepeat, 0x48)) Log.Warning("Global shortcut Ctrl+Shift+H is unavailable");
        if (!_interop.RegisterHotKey(3, ModControl | ModAlt | ModNoRepeat, 0x50)) Log.Warning("Global shortcut Ctrl+Alt+P is unavailable");
    }

    private void OnHotKeyPressed(object? sender, int id)
    {
        if (id is 1 or 2) _paste.RememberTarget();
        if (id == 1) ShowCompact(); else if (id == 2) Activate(); else if (id == 3) { _isPinned = !_isPinned; ApplyPinned(); _ = SavePinAsync(); }
    }

    public void ShowCompact() { _compact ??= new CompactWindow(); _compact.Closed += (_, _) => _compact = null; _compact.Activate(); }
    public void ShowMainWindow()
    {
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter && presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized) presenter.Restore();
        AppWindow.Show();
        Activate();
    }
    public void ApplyTheme(string theme)
    {
        WindowRoot.RequestedTheme = theme switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        ThemeIcon.Glyph = theme == "Light" ? "\uE706" : "\uE708";
        ConfigureCaptionButtons();
    }
    private void TitleSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && RootFrame.Content is MainPage page) page.SetSearchText(sender.Text);
    }
    private void SearchAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { TitleSearch.Focus(FocusState.Keyboard); args.Handled = true; }
    private async void TitleSettings_Click(object sender, RoutedEventArgs e) { if (RootFrame.Content is MainPage page) await page.ShowSettingsAsync(); }
    private async void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        var theme = WindowRoot.ActualTheme == ElementTheme.Dark ? "Light" : "Dark";
        await _monitor.UpdateSettingsAsync(_monitor.Settings with { Theme = theme });
        ApplyTheme(theme);
    }
    private async void PinButton_Click(object sender, RoutedEventArgs e) { _isPinned = !_isPinned; ApplyPinned(); await SavePinAsync(); }
    private void ApplyPinned()
    {
        _interop.SetTopmost(_isPinned); PinIcon.Glyph = _isPinned ? "\uE841" : "\uE840";
        ToolTipService.SetToolTip(PinButton, $"Always on Top: {(_isPinned ? "Enabled" : "Disabled")}");
        PinButton.Foreground = _isPinned ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.DeepSkyBlue) : null;
    }

    private void ConfigureCaptionButtons()
    {
        var isDark = AppTitleBar.ActualTheme == ElementTheme.Dark;
        var foreground = isDark ? Colors.White : ColorHelper.FromArgb(255, 17, 24, 39);
        var inactive = isDark ? ColorHelper.FromArgb(180, 255, 255, 255) : ColorHelper.FromArgb(170, 17, 24, 39);
        var hover = isDark ? ColorHelper.FromArgb(35, 255, 255, 255) : ColorHelper.FromArgb(20, 17, 24, 39);

        AppWindow.TitleBar.ButtonForegroundColor = foreground;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = inactive;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonHoverForegroundColor = foreground;
        AppWindow.TitleBar.ButtonHoverBackgroundColor = hover;
        AppWindow.TitleBar.ButtonPressedForegroundColor = foreground;
        AppWindow.TitleBar.ButtonPressedBackgroundColor = hover;
    }
    private Task SavePinAsync() => _monitor.UpdateSettingsAsync(_monitor.Settings with { MainWindowAlwaysOnTop = _isPinned });

    private void ConfigureTrayIcon()
    {
        _interop.AddTrayIcon("ClipVault Clipboard Manager", Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        _interop.TrayCommand += (_, command) => DispatcherQueue.TryEnqueue(async () =>
        {
            if (command == 1) ShowMainWindow();
            else if (command == 2) ShowCompact();
            else if (command == 3) await _monitor.UpdateSettingsAsync(_monitor.Settings with { MonitoringEnabled = !_monitor.Settings.MonitoringEnabled });
            else if (command == 4) await _monitor.UpdateSettingsAsync(_monitor.Settings with { PrivateMode = !_monitor.Settings.PrivateMode });
            else if (command == 5) { _exitRequested = true; Close(); }
        });
    }

    private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (!_exitRequested && _monitor.Settings.CloseToTray)
        {
            args.Cancel = true;
            sender.Hide();
        }
    }

    private void AppWindow_Changed(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
    {
        if (args.DidPresenterChange && _monitor.Settings.MinimizeToTray && sender.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter && presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized)
            sender.Hide();
    }
}
