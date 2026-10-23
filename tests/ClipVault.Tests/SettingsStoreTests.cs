using ClipVault.Core.Models;
using ClipVault.Core.Services;

namespace ClipVault.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task Saves_And_Loads_Independent_Pin_State()
    {
        var path = Path.Combine(Path.GetTempPath(), "ClipVaultTests", Guid.NewGuid().ToString("N"), "clipvault.db");
        var store = new SettingsStore(path);
        await store.SaveAsync(new AppSettings { MainWindowAlwaysOnTop = false, CompactWindowAlwaysOnTop = true, StartWithWindows = true, ExcludedApplications = ["Secret.exe"] });
        var loaded = await store.LoadAsync();
        Assert.False(loaded.MainWindowAlwaysOnTop);
        Assert.True(loaded.CompactWindowAlwaysOnTop);
        Assert.True(loaded.StartWithWindows);
        Assert.Equal(["Secret.exe"], loaded.ExcludedApplications);
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [Fact]
    public async Task Migrates_Legacy_Json_Into_Local_Sqlite()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ClipVaultTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var legacy = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(legacy, "{\"Theme\":\"Light\",\"PrivateMode\":true}");
        var store = new SettingsStore(Path.Combine(directory, "clipvault.db"), legacy);
        var migrated = await store.LoadAsync();
        Assert.Equal("Light", migrated.Theme);
        Assert.True(migrated.PrivateMode);
        File.Delete(legacy);
        Assert.Equal("Light", (await store.LoadAsync()).Theme);
        Directory.Delete(directory, true);
    }
}
