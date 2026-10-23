using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using ClipVault.Core.Models;
using ClipVault.Core.Services;
using Serilog;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ClipVault_App.Services;

public sealed class ClipboardMonitorService(ClipboardRepository repository, SettingsStore settingsStore)
{
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private AppSettings _settings = new();
    public event EventHandler? HistoryChanged;
    public bool IsMonitoring => _settings.MonitoringEnabled && !_settings.PrivateMode;
    public AppSettings Settings => _settings;

    public async Task InitializeAsync() { _settings = await settingsStore.LoadAsync(); await repository.InitializeAsync(); await repository.CleanupAsync(_settings.RetentionDays, _settings.MaximumHistoryItems); }
    public async Task UpdateSettingsAsync(AppSettings settings) { _settings = settings; await settingsStore.SaveAsync(settings); }

    public async Task CaptureAsync()
    {
        if (!IsMonitoring || !await _captureGate.WaitAsync(0)) return;
        try
        {
            var sourceApplication = GetClipboardSourceApplication();
            if (sourceApplication is not null && _settings.ExcludedApplications.Any(x => string.Equals(Path.GetFileName(x.Trim()), sourceApplication, StringComparison.OrdinalIgnoreCase))) return;
            DataPackageView? view = null;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                try { view = Clipboard.GetContent(); break; }
                catch (COMException) when (attempt < 3) { await Task.Delay(20 * (1 << attempt)); }
            }
            if (view is null) return;
            ClipboardItem? item = null;
            if (view.Contains(StandardDataFormats.Text))
            {
                var text = await view.GetTextAsync();
                if (Encoding.UTF8.GetByteCount(text) <= _settings.MaximumItemBytes)
                    item = new ClipboardItem { ContentType = ContentUtilities.IsUrl(text) ? ClipboardContentType.Link : ClipboardContentType.Text, Preview = ContentUtilities.Preview(text), TextContent = text, ContentHash = ContentUtilities.Sha256(text), SourceApplication = sourceApplication };
            }
            else if (view.Contains(StandardDataFormats.StorageItems))
            {
                var paths = (await view.GetStorageItemsAsync()).Select(x => x.Path).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                var text = string.Join(Environment.NewLine, paths);
                if (Encoding.UTF8.GetByteCount(text) <= _settings.MaximumItemBytes)
                    item = new ClipboardItem { ContentType = ClipboardContentType.Files, Preview = ContentUtilities.Preview(text), TextContent = text, ContentHash = ContentUtilities.Sha256(text), SourceApplication = sourceApplication };
            }
            else if (view.Contains(StandardDataFormats.Bitmap)) item = (await CaptureBitmapAsync(view)) is { } image ? image with { SourceApplication = sourceApplication } : null;
            if (item is not null && await repository.AddAsync(item, _settings.DuplicateDetection)) HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { Log.Warning(ex, "Clipboard capture failed; no clipboard payload was logged"); }
        finally { _captureGate.Release(); }
    }

    private async Task<ClipboardItem?> CaptureBitmapAsync(DataPackageView view)
    {
        var reference = await view.GetBitmapAsync();
        using var source = await reference.OpenReadAsync();
        if ((long)source.Size > _settings.MaximumItemBytes) return null;
        var bytes = new byte[checked((int)source.Size)];
        using var reader = new DataReader(source); await reader.LoadAsync((uint)source.Size); reader.ReadBytes(bytes);
        var hash = ContentUtilities.Sha256(bytes);
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipVault", "Images");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, hash + ".png");
        if (!File.Exists(path)) await File.WriteAllBytesAsync(path, bytes);
        return new ClipboardItem { ContentType = ClipboardContentType.Image, Preview = $"Image · {source.Size:N0} bytes", BinaryPath = path, ContentHash = hash };
    }

    public async Task CopyAsync(ClipboardItem item)
    {
        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        if (item.TextContent is not null) package.SetText(item.TextContent);
        else if (item.BinaryPath is not null && File.Exists(item.BinaryPath)) package.SetBitmap(RandomAccessStreamReference.CreateFromFile(await StorageFile.GetFileFromPathAsync(item.BinaryPath)));
        else return;
        Clipboard.SetContent(package); Clipboard.Flush();
    }

    private static string? GetClipboardSourceApplication()
    {
        var owner = GetClipboardOwner();
        if (owner == 0) return null;
        GetWindowThreadProcessId(owner, out var processId);
        if (processId == 0) return null;
        try { return Process.GetProcessById((int)processId).ProcessName + ".exe"; }
        catch { return null; }
    }

    [DllImport("user32.dll")] private static extern nint GetClipboardOwner();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
}
