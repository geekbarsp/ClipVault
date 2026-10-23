using ClipVault.Core.Models;
using ClipVault.Core.Services;

namespace ClipVault.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task Saves_And_Loads_Independent_Pin_State()
    {
        var path = Path.Combine(Path.GetTempPath(), "ClipVaultTests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new SettingsStore(path);
        await store.SaveAsync(new AppSettings { MainWindowAlwaysOnTop = false, CompactWindowAlwaysOnTop = true, StartWithWindows = true, ExcludedApplications = ["Secret.exe"] });
        var loaded = await store.LoadAsync();
        Assert.False(loaded.MainWindowAlwaysOnTop);
        Assert.True(loaded.CompactWindowAlwaysOnTop);
        Assert.True(loaded.StartWithWindows);
        Assert.Equal(["Secret.exe"], loaded.ExcludedApplications);
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }
}
