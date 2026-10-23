using ClipVault.Core.Models;
using ClipVault.Core.Services;
using ClipVault_App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace ClipVault_App;

public sealed partial class CompactWindow : Window
{
    private readonly ClipboardRepository _repository = App.Services.GetRequiredService<ClipboardRepository>();
    private readonly ClipboardMonitorService _monitor = App.Services.GetRequiredService<ClipboardMonitorService>();
    private readonly SafePasteService _paste = App.Services.GetRequiredService<SafePasteService>();
    private readonly WindowInteropService _interop;
    private ClipboardItem? _selected;
    private bool _isPinned;
    private bool _showPinnedOnly;
    private CancellationTokenSource? _copyStatusCancellation;

    public CompactWindow()
    {
        InitializeComponent(); ExtendsContentIntoTitleBar = true; SetTitleBar(CompactTitleBar);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(420, 600));
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.IsMaximizable = false;
        _interop = new WindowInteropService(this); _isPinned = _monitor.Settings.CompactWindowAlwaysOnTop; ApplyPin(); ApplyTheme();
        Activated += async (_, _) => { ApplyTheme(); await LoadAsync(SearchBox.Text); SearchBox.Focus(FocusState.Keyboard); }; Closed += (_, _) => { _copyStatusCancellation?.Cancel(); _copyStatusCancellation?.Dispose(); _interop.Dispose(); };
    }

    private void ApplyTheme() => CompactRoot.RequestedTheme = _monitor.Settings.Theme switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
    private async Task LoadAsync(string? query) { ItemsList.ItemsSource = await _repository.SearchAsync(query, pinned: _showPinnedOnly ? true : null, limit: 100); }
    private async void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) { if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) await LoadAsync(sender.Text); }
    private void Selection_Changed(object sender, SelectionChangedEventArgs e) => _selected = ItemsList.SelectedItem as ClipboardItem;
    private async void AllItems_Click(object sender, RoutedEventArgs e) { _showPinnedOnly = false; AllItemsButton.IsChecked = true; PinnedItemsButton.IsChecked = false; await LoadAsync(SearchBox.Text); }
    private async void PinnedItems_Click(object sender, RoutedEventArgs e) { _showPinnedOnly = true; AllItemsButton.IsChecked = false; PinnedItemsButton.IsChecked = true; await LoadAsync(SearchBox.Text); }
    private async void ItemsList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        _selected = ItemsList.SelectedItem as ClipboardItem;
        if (_selected is not null)
        {
            await _monitor.CopyAsync(_selected);
            ShowCopiedIndicator(_selected);
        }
    }
    private async void Copy_Click(object sender, RoutedEventArgs e) { if (_selected is not null) { await _monitor.CopyAsync(_selected); ShowCopiedIndicator(_selected); } }
    private async void Paste_Click(object sender, RoutedEventArgs e) { if (_selected is not null) { AppWindow.Hide(); await _paste.PasteAsync(_selected); } }
    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Down && ItemsList.Items.Count > 0) { ItemsList.SelectedIndex = Math.Max(0, ItemsList.SelectedIndex); ItemsList.Focus(FocusState.Keyboard); e.Handled = true; }
        else if (e.Key == VirtualKey.Enter && ItemsList.Items.Count > 0) { ItemsList.SelectedIndex = Math.Max(0, ItemsList.SelectedIndex); _selected = ItemsList.SelectedItem as ClipboardItem; _ = PasteSelectedAsync(); e.Handled = true; }
        else if (e.Key == VirtualKey.Escape) { AppWindow.Hide(); e.Handled = true; }
    }
    private void ItemsList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) { _ = PasteSelectedAsync(); e.Handled = true; }
        else if (e.Key == VirtualKey.Escape) { AppWindow.Hide(); e.Handled = true; }
    }
    private async Task PasteSelectedAsync() { if (_selected is not null) { AppWindow.Hide(); await _paste.PasteAsync(_selected); } }
    private async void Pin_Click(object sender, RoutedEventArgs e) { _isPinned = !_isPinned; ApplyPin(); await _monitor.UpdateSettingsAsync(_monitor.Settings with { CompactWindowAlwaysOnTop = _isPinned }); }
    private void ApplyPin() { _interop.SetTopmost(_isPinned); CompactPinIcon.Glyph = _isPinned ? "\uE841" : "\uE840"; ToolTipService.SetToolTip(CompactPinButton, $"Always on Top: {(_isPinned ? "Enabled" : "Disabled")}"); }
    private void Expand_Click(object sender, RoutedEventArgs e) { App.Window.Activate(); AppWindow.Hide(); }

    private async void ShowCopiedIndicator(ClipboardItem item)
    {
        _copyStatusCancellation?.Cancel();
        _copyStatusCancellation?.Dispose();
        _copyStatusCancellation = new CancellationTokenSource();
        var cancellationToken = _copyStatusCancellation.Token;
        CopyStatusText.Text = $"Copied: {item.DisplayTitle}";
        CopyStatus.Visibility = Visibility.Visible;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1.8), cancellationToken);
            CopyStatus.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
    }
}
