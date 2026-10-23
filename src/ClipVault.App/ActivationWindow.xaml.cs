using ClipVault_App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace ClipVault_App;

public sealed partial class ActivationWindow : Window
{
    private readonly LicenseService _licenses = App.Services.GetRequiredService<LicenseService>();

    public ActivationWindow(string? message = null)
    {
        InitializeComponent();
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(560, 620));
        if (!string.IsNullOrWhiteSpace(message)) { StatusBar.Message = message; StatusBar.IsOpen = true; }
    }

    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        ActivateButton.IsEnabled = false; Progress.Visibility = Visibility.Visible; Progress.IsActive = true; StatusBar.IsOpen = false; ConnectionStatus.Text = "Internet: contacting licensing server…";
        try
        {
            var result = await _licenses.ActivateAsync(LicenseKeyBox.Text);
            ConnectionStatus.Text = "Internet: connected";
            if (result.Allowed) { await App.OpenMainWindowAsync(); Close(); return; }
            StatusBar.Message = result.Message; StatusBar.IsOpen = true;
        }
        catch (HttpRequestException) { ConnectionStatus.Text = "Internet: unavailable"; StatusBar.Message = "Unable to contact the licensing server. Please check your internet connection."; StatusBar.IsOpen = true; }
        finally { ActivateButton.IsEnabled = true; Progress.IsActive = false; Progress.Visibility = Visibility.Collapsed; }
    }
}
