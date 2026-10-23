using ClipVault.Core.Models;
using ClipVault.Core.Services;
using ClipVault_App.Services;
using ClipVault_App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ClipVault_App;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; } = App.Services.GetRequiredService<MainPageViewModel>();
    private ClipboardMonitorService Monitor { get; } = App.Services.GetRequiredService<ClipboardMonitorService>();
    private ClipboardRepository Repository { get; } = App.Services.GetRequiredService<ClipboardRepository>();
    private StartupService Startup { get; } = App.Services.GetRequiredService<StartupService>();
    private BackupService Backup { get; } = App.Services.GetRequiredService<BackupService>();
    private ImageOcrService Ocr { get; } = App.Services.GetRequiredService<ImageOcrService>();
    private bool _selectionMode;
    private bool _settingsDialogOpen;
    public MainPage() => InitializeComponent();
    private async void Page_Loaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();
    private async void Section_Changed(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as ListView)?.SelectedItem is ListViewItem item && item.Tag is string section) await ViewModel.ApplySectionAsync(section);
    }

    private async void Sort_Changed(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as ComboBox)?.SelectedItem is ComboBoxItem item && item.Tag is string value && Enum.TryParse<ClipboardSortOrder>(value, out var order)) await ViewModel.ApplySortAsync(order);
    }

    private void History_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectionMode) UpdateSelectedCount();
        PreviewImage.Source = null;
        if (ViewModel.SelectedItem?.BinaryPath is { } path && File.Exists(path))
        {
            try { PreviewImage.Source = new BitmapImage(new Uri(path)); } catch { }
        }
    }

    private void SelectMode_Click(object sender, RoutedEventArgs e)
    {
        _selectionMode = true;
        HistoryList.SelectionMode = ListViewSelectionMode.Multiple;
        HistoryList.SelectedItems.Clear();
        SelectModeButton.Visibility = Visibility.Collapsed;
        BatchActions.Visibility = Visibility.Visible;
        UpdateSelectedCount();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        HistoryList.SelectAll();
        UpdateSelectedCount();
    }

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = HistoryList.SelectedItems.OfType<ClipboardItem>().ToArray();
        if (selectedItems.Length == 0) return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Delete {selectedItems.Length} selected item{(selectedItems.Length == 1 ? "" : "s")}?",
            Content = "The selected clipboard items will be moved to Trash.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await ViewModel.DeleteItemsAsync(selectedItems);
        ExitSelectionMode();
    }

    private void CancelSelection_Click(object sender, RoutedEventArgs e) => ExitSelectionMode();

    private void ExitSelectionMode()
    {
        HistoryList.SelectedItems.Clear();
        HistoryList.SelectionMode = ListViewSelectionMode.Single;
        _selectionMode = false;
        BatchActions.Visibility = Visibility.Collapsed;
        SelectModeButton.Visibility = Visibility.Visible;
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        var count = _selectionMode ? HistoryList.SelectedItems.Count : 0;
        SelectedCountText.Text = $"{count} selected";
    }

    private async void FavoriteItem_Click(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.DataContext is ClipboardItem item) await ViewModel.ToggleFavoriteForAsync(item); }
    private async void PinItem_Click(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.DataContext is ClipboardItem item) await TogglePinWithTitleAsync(item); }
    private async void PinSelected_Click(object sender, RoutedEventArgs e) { if (ViewModel.SelectedItem is { } item) await TogglePinWithTitleAsync(item); }
    private async void EditPinTitle_Click(object sender, RoutedEventArgs e) { if ((sender as MenuFlyoutItem)?.Tag is ClipboardItem item) await ShowPinTitleDialogAsync(item); }
    private async void CopyItem_Click(object sender, RoutedEventArgs e) { if ((sender as MenuFlyoutItem)?.Tag is ClipboardItem item) await ViewModel.CopyItemAsync(item); }
    private async void DeleteItem_Click(object sender, RoutedEventArgs e) { if ((sender as MenuFlyoutItem)?.Tag is ClipboardItem item) await ViewModel.DeleteItemAsync(item); }
    private async void EditText_Click(object sender, RoutedEventArgs e) { if ((sender as MenuFlyoutItem)?.Tag is ClipboardItem item) await ShowEditTextDialogAsync(item); }
    private async void OrganizeItem_Click(object sender, RoutedEventArgs e) { if ((sender as MenuFlyoutItem)?.Tag is ClipboardItem item) await ShowOrganizationDialogAsync(item); }
    private async void ExtractOcr_Click(object sender, RoutedEventArgs e) { if ((sender as MenuFlyoutItem)?.Tag is ClipboardItem item) await ExtractOcrAsync(item); }

    private async Task TogglePinWithTitleAsync(ClipboardItem item)
    {
        if (item.IsPinned)
        {
            await ViewModel.SetPinnedForAsync(item, false);
            return;
        }

        await ShowPinTitleDialogAsync(item);
    }

    private async Task ShowPinTitleDialogAsync(ClipboardItem item)
    {
        var titleBox = new TextBox
        {
            Header = "Title",
            PlaceholderText = "Example: Wi-Fi password or project note",
            Text = item.Title ?? string.Empty,
            MaxLength = 120,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var content = new StackPanel { Spacing = 10, MinWidth = 320 };
        content.Children.Add(new TextBlock
        {
            Text = "Give this pinned item a short name. The copied content will not be changed.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72
        });
        content.Children.Add(titleBox);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = item.IsPinned ? "Edit pin title" : "Pin clipboard item",
            Content = content,
            PrimaryButtonText = item.IsPinned ? "Save" : "Pin",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.SetPinnedForAsync(item, true, titleBox.Text);
    }

    private async Task ShowEditTextDialogAsync(ClipboardItem item)
    {
        if (item.TextContent is null) { await ShowMessageAsync("Edit text", "This clipboard item does not contain editable text."); return; }
        var editor = new TextBox { Text = item.TextContent, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinWidth = 420, MinHeight = 220, MaxHeight = 480 };
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Edit clipboard text", Content = editor, PrimaryButtonText = "Save", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(editor.Text)) await ViewModel.UpdateTextAsync(item, editor.Text);
    }

    private async Task ShowOrganizationDialogAsync(ClipboardItem item)
    {
        var collection = new TextBox { Header = "Collection", Text = item.CollectionName ?? string.Empty, PlaceholderText = "Work, Personal, Code..." };
        var tags = new TextBox { Header = "Tags (comma separated)", Text = string.Join(", ", item.Tags), PlaceholderText = "important, account, project" };
        var panel = new StackPanel { Spacing = 12, MinWidth = 360 }; panel.Children.Add(collection); panel.Children.Add(tags);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Collection and tags", Content = panel, PrimaryButtonText = "Save", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.OrganizeAsync(item, collection.Text, tags.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private async Task ExtractOcrAsync(ClipboardItem item)
    {
        if (item.ContentType != ClipboardContentType.Image || item.BinaryPath is null || !File.Exists(item.BinaryPath)) { await ShowMessageAsync("Image OCR", "Select an image clipboard item first."); return; }
        try
        {
            var text = await Ocr.ExtractAsync(item.BinaryPath);
            if (string.IsNullOrWhiteSpace(text)) await ShowMessageAsync("Image OCR", "No readable text was found in this image.");
            else { await ViewModel.SaveOcrTextAsync(item, text); await ShowMessageAsync("Image OCR", text); }
        }
        catch (Exception ex) { await ShowMessageAsync("Image OCR", ex.Message); }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        await new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = message, CloseButtonText = "Close" }.ShowAsync();
    }

    private async void PrivateMode_Toggled(object sender, RoutedEventArgs e)
    {
        if (PrivateToggle.IsOn != ViewModel.PrivateMode) await ViewModel.TogglePrivateModeCommand.ExecuteAsync(null);
    }

    private void CompactMode_Click(object sender, RoutedEventArgs e) => (App.Window as MainWindow)?.ShowCompact();
    public void SetSearchText(string value) => ViewModel.SearchText = value;

    private async void Settings_Click(object sender, RoutedEventArgs e) => await ShowSettingsAsync();

    public async Task ShowSettingsAsync()
    {
        if (_settingsDialogOpen) return;
        _settingsDialogOpen = true;
        try
        {
            await ShowSettingsDialogAsync();
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = $"Settings could not open: {ex.Message}";
        }
        finally
        {
            _settingsDialogOpen = false;
        }
    }

    private async Task ShowSettingsDialogAsync()
    {
        var monitoring = new CheckBox { Content = "Capture clipboard changes", IsChecked = Monitor.Settings.MonitoringEnabled };
        var duplicate = new CheckBox { Content = "Group duplicate copies", IsChecked = Monitor.Settings.DuplicateDetection };
        var startWithWindows = new CheckBox { Content = "Start ClipVault with Windows", IsChecked = Monitor.Settings.StartWithWindows };
        var minimizeToTray = new CheckBox { Content = "Minimize to system tray", IsChecked = Monitor.Settings.MinimizeToTray };
        var closeToTray = new CheckBox { Content = "Close to system tray", IsChecked = Monitor.Settings.CloseToTray };
        var retention = new NumberBox { Header = "Delete ordinary history after this many days", Value = Monitor.Settings.RetentionDays, Minimum = 1, Maximum = 3650, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var maximum = new NumberBox { Header = "Maximum ordinary history items", Value = Monitor.Settings.MaximumHistoryItems, Minimum = 100, Maximum = 100000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var excluded = new TextBox { Header = "Never capture from these apps (one .exe per line)", Text = string.Join(Environment.NewLine, Monitor.Settings.ExcludedApplications ?? []), AcceptsReturn = true, MinHeight = 100, TextWrapping = TextWrapping.Wrap };
        var theme = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        theme.Items.Add(new ComboBoxItem { Content = "Follow Windows", Tag = "System" });
        theme.Items.Add(new ComboBoxItem { Content = "Light", Tag = "Light" });
        theme.Items.Add(new ComboBoxItem { Content = "Dark", Tag = "Dark" });
        theme.SelectedIndex = Monitor.Settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        var content = new StackPanel { Spacing = 10, MinWidth = 320, MaxWidth = 440 };
        content.Children.Add(new TextBlock { Text = "APPEARANCE", Opacity = 0.65, FontSize = 12 });
        content.Children.Add(new TextBlock { Text = "Theme" }); content.Children.Add(theme);
        content.Children.Add(new TextBlock { Text = "WINDOWS", Opacity = 0.65, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) });
        content.Children.Add(startWithWindows); content.Children.Add(minimizeToTray); content.Children.Add(closeToTray);
        content.Children.Add(new TextBlock { Text = "CLIPBOARD", Opacity = 0.65, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) });
        content.Children.Add(monitoring); content.Children.Add(duplicate); content.Children.Add(retention); content.Children.Add(maximum); content.Children.Add(excluded);
        content.Children.Add(new TextBlock { Text = "BACKUP AND RESTORE", Opacity = 0.65, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) });
        var backupStatus = new TextBlock { Opacity = 0.72, TextWrapping = TextWrapping.Wrap };
        var backupButton = new Button { Content = "Export backup..." };
        var restoreButton = new Button { Content = "Restore backup..." };
        var backupActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }; backupActions.Children.Add(backupButton); backupActions.Children.Add(restoreButton);
        content.Children.Add(backupActions); content.Children.Add(backupStatus);
        backupButton.Click += async (_, _) =>
        {
            var picker = new FileSavePicker { SuggestedFileName = $"ClipVault-backup-{DateTime.Now:yyyy-MM-dd}", SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeChoices.Add("ClipVault backup", new List<string> { ".clipvault" });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Window));
            if (await picker.PickSaveFileAsync() is { } file) { await Backup.CreateAsync(file.Path); backupStatus.Text = "Backup created successfully."; }
        };
        restoreButton.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary }; picker.FileTypeFilter.Add(".clipvault");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Window));
            if (await picker.PickSingleFileAsync() is { } file) { await Backup.RestoreAsync(file.Path); await Monitor.InitializeAsync(); await ViewModel.RefreshCommand.ExecuteAsync(null); backupStatus.Text = "Backup restored successfully."; }
        };
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "ClipVault settings", Content = new ScrollViewer { Content = content, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, PrimaryButtonText = "Save", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var selectedTheme = (theme.SelectedItem as ComboBoxItem)?.Tag as string ?? "System";
            var updated = Monitor.Settings with
            {
                MonitoringEnabled = monitoring.IsChecked == true,
                DuplicateDetection = duplicate.IsChecked == true,
                StartWithWindows = startWithWindows.IsChecked == true,
                MinimizeToTray = minimizeToTray.IsChecked == true,
                CloseToTray = closeToTray.IsChecked == true,
                RetentionDays = Math.Clamp((int)retention.Value, 1, 3650),
                MaximumHistoryItems = Math.Clamp((int)maximum.Value, 100, 100000),
                ExcludedApplications = excluded.Text.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                Theme = selectedTheme
            };
            await Monitor.UpdateSettingsAsync(updated);
            Startup.Apply(updated.StartWithWindows);
            await Repository.CleanupAsync(updated.RetentionDays, updated.MaximumHistoryItems);
            (App.Window as MainWindow)?.ApplyTheme(selectedTheme);
            await ViewModel.RefreshCommand.ExecuteAsync(null);
        }
    }

    private async void About_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "About ClipVault", Content = "ClipVault 1.0.0\nAdvanced Clipboard Manager for Windows\n\nYour clipboard history stays on this device.", CloseButtonText = "Close" };
        await dialog.ShowAsync();
    }
}
