using System.Collections.ObjectModel;
using ClipVault.Core.Models;
using ClipVault.Core.Services;
using ClipVault_App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipVault_App.ViewModels;

public partial class MainPageViewModel(ClipboardRepository repository, ClipboardMonitorService monitor, SafePasteService paste) : ObservableObject
{
    private ClipboardContentType? _typeFilter;
    private bool? _pinnedFilter;
    private bool? _favoriteFilter;
    private bool _showTrash;
    private ClipboardSortOrder _sortOrder;
    public ObservableCollection<ClipboardItem> Items { get; } = [];
    [ObservableProperty] public partial ClipboardItem? SelectedItem { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial string StatusText { get; set; } = "Ready";
    [ObservableProperty] public partial bool MonitoringEnabled { get; set; } = monitor.IsMonitoring;
    [ObservableProperty] public partial bool PrivateMode { get; set; } = monitor.Settings.PrivateMode;

    public async Task LoadAsync() { await RefreshAsync(); monitor.HistoryChanged += OnHistoryChanged; }
    partial void OnSearchTextChanged(string value) => _ = RefreshAsync();
    private void OnHistoryChanged(object? sender, EventArgs e) => _ = RefreshAsync();

    [RelayCommand] private async Task RefreshAsync()
    {
        try
        {
            var items = await repository.SearchAsync(SearchText, _typeFilter, _pinnedFilter, _favoriteFilter, _showTrash, sort: _sortOrder);
            Items.Clear(); foreach (var item in items) Items.Add(item);
            StatusText = $"{Items.Count} item{(Items.Count == 1 ? "" : "s")}";
        }
        catch { StatusText = "Search could not be completed"; }
    }

    public async Task ApplySectionAsync(string section)
    {
        _typeFilter = section switch { "Text" => ClipboardContentType.Text, "Images" => ClipboardContentType.Image, "Links" => ClipboardContentType.Link, "Files" => ClipboardContentType.Files, _ => null };
        _pinnedFilter = section == "Pinned" ? true : null;
        _favoriteFilter = section == "Favorites" ? true : null;
        _showTrash = section == "Trash";
        await RefreshAsync();
    }

    public async Task ApplySortAsync(ClipboardSortOrder sortOrder) { _sortOrder = sortOrder; await RefreshAsync(); }
    public async Task SetPinnedForAsync(ClipboardItem item, bool isPinned, string? title = null) { await repository.SetPinnedAsync(item.Id, isPinned, title); await RefreshAsync(); }
    public async Task ToggleFavoriteForAsync(ClipboardItem item) { await repository.SetFavoriteAsync(item.Id, !item.IsFavorite); await RefreshAsync(); }
    public async Task DeleteItemAsync(ClipboardItem item) { await repository.MoveToTrashAsync(item.Id); await RefreshAsync(); }
    public async Task DeleteItemsAsync(IEnumerable<ClipboardItem> items)
    {
        var selected = items.ToArray();
        foreach (var item in selected) await repository.MoveToTrashAsync(item.Id);
        await RefreshAsync();
        StatusText = $"{selected.Length} item{(selected.Length == 1 ? "" : "s")} moved to Trash";
    }
    public async Task CopyItemAsync(ClipboardItem item) { await monitor.CopyAsync(item); StatusText = "Copied"; }
    public async Task UpdateTextAsync(ClipboardItem item, string text) { await repository.UpdateTextAsync(item.Id, text); await RefreshAsync(); StatusText = "Clipboard text updated"; }
    public async Task OrganizeAsync(ClipboardItem item, string? collection, IEnumerable<string> tags) { await repository.SetOrganizationAsync(item.Id, collection, tags); await RefreshAsync(); StatusText = "Organization saved"; }
    public async Task SaveOcrTextAsync(ClipboardItem item, string text) { await repository.SetOcrTextAsync(item.Id, text); await RefreshAsync(); StatusText = "Image text extracted"; }

    [RelayCommand(CanExecute = nameof(HasSelection))] private async Task CopyAsync() { if (SelectedItem is null) return; await monitor.CopyAsync(SelectedItem); StatusText = "Copied"; }
    [RelayCommand(CanExecute = nameof(HasSelection))] private async Task PasteAsync() { if (SelectedItem is null) return; StatusText = await paste.PasteAsync(SelectedItem) ? "Pasted" : "Copied — automatic paste was not safe"; }
    [RelayCommand(CanExecute = nameof(HasSelection))] private async Task ToggleFavoriteAsync() { if (SelectedItem is null) return; await repository.SetFavoriteAsync(SelectedItem.Id, !SelectedItem.IsFavorite); await RefreshAsync(); }
    [RelayCommand(CanExecute = nameof(HasSelection))] private async Task DeleteAsync() { if (SelectedItem is null) return; await repository.MoveToTrashAsync(SelectedItem.Id); await RefreshAsync(); }

    [RelayCommand] private async Task ToggleMonitoringAsync()
    {
        var desired = !monitor.Settings.MonitoringEnabled;
        await monitor.UpdateSettingsAsync(monitor.Settings with { MonitoringEnabled = desired });
        MonitoringEnabled = monitor.IsMonitoring;
        StatusText = MonitoringEnabled ? "Clipboard monitoring resumed" : "Clipboard monitoring paused";
    }

    [RelayCommand] private async Task TogglePrivateModeAsync()
    {
        PrivateMode = !PrivateMode;
        await monitor.UpdateSettingsAsync(monitor.Settings with { PrivateMode = PrivateMode });
        MonitoringEnabled = monitor.IsMonitoring;
        StatusText = PrivateMode ? "Private mode — nothing will be captured" : "Private mode disabled";
    }

    private bool HasSelection() => SelectedItem is not null;
    partial void OnSelectedItemChanged(ClipboardItem? value) { CopyCommand.NotifyCanExecuteChanged(); PasteCommand.NotifyCanExecuteChanged(); ToggleFavoriteCommand.NotifyCanExecuteChanged(); DeleteCommand.NotifyCanExecuteChanged(); }
}
