using ClipVault.Core.Models;
using ClipVault.Core.Services;

namespace ClipVault.Tests;

public sealed class ClipboardRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ClipVaultTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Persists_Searches_And_Filters_Items()
    {
        var repository = await CreateAsync();
        var item = NewItem("The quick brown fox", pinned: true);
        Assert.True(await repository.AddAsync(item));
        var found = await repository.SearchAsync("quick brown", pinned: true);
        Assert.Single(found);
        Assert.Equal(item.Id, found[0].Id);
    }

    [Fact]
    public async Task Duplicate_Hash_Is_Grouped()
    {
        var repository = await CreateAsync();
        Assert.True(await repository.AddAsync(NewItem("same")));
        Assert.True(await repository.AddAsync(NewItem("same")));
        var grouped = Assert.Single(await repository.SearchAsync());
        Assert.Equal(2, grouped.DuplicateCount);
    }

    [Fact]
    public async Task Pin_Favorite_Trash_And_Restore_Are_Independent()
    {
        var repository = await CreateAsync();
        var item = NewItem("state");
        await repository.AddAsync(item);
        await repository.SetPinnedAsync(item.Id, true);
        await repository.SetFavoriteAsync(item.Id, true);
        await repository.MoveToTrashAsync(item.Id);
        Assert.Empty(await repository.SearchAsync());
        Assert.Single(await repository.SearchAsync(includeDeleted: true, pinned: true, favorite: true));
        await repository.RestoreAsync(item.Id);
        Assert.Single(await repository.SearchAsync(pinned: true, favorite: true));
    }

    [Fact]
    public async Task Pin_Title_Is_Persisted_And_Searchable()
    {
        var databasePath = Path.Combine(_directory, "restart-test.db");
        var repository = new ClipboardRepository(databasePath);
        await repository.InitializeAsync();
        var item = NewItem("original clipboard text");
        await repository.AddAsync(item);
        await repository.SetPinnedAsync(item.Id, true, "Important account note");

        // A new repository instance simulates the app starting again after a PC restart.
        var reopenedRepository = new ClipboardRepository(databasePath);
        await reopenedRepository.InitializeAsync();
        var found = await reopenedRepository.SearchAsync("account note", pinned: true);

        Assert.Single(found);
        Assert.Equal("Important account note", found[0].Title);
        Assert.Equal("Important account note", found[0].DisplayTitle);
        Assert.Equal("original clipboard text", found[0].TextContent);
    }

    [Fact]
    public async Task Cleanup_Never_Removes_Pinned_Items()
    {
        var repository = await CreateAsync();
        await repository.AddAsync(NewItem("old pinned", pinned: true) with { CreatedAt = DateTimeOffset.UtcNow.AddYears(-1) });
        await repository.AddAsync(NewItem("old normal") with { CreatedAt = DateTimeOffset.UtcNow.AddYears(-1) });
        await repository.CleanupAsync(30, 10);
        var remaining = await repository.SearchAsync();
        Assert.Single(remaining);
        Assert.True(remaining[0].IsPinned);
    }

    [Fact]
    public async Task Editing_Collection_And_Tags_Are_Persisted()
    {
        var repository = await CreateAsync();
        var item = NewItem("draft");
        await repository.AddAsync(item);
        await repository.UpdateTextAsync(item.Id, "finished text");
        await repository.SetOrganizationAsync(item.Id, "Work", ["urgent", "client"]);

        var found = Assert.Single(await repository.SearchAsync("urgent"));
        Assert.Equal("finished text", found.TextContent);
        Assert.Equal("Work", found.CollectionName);
        Assert.Contains("client", found.Tags);
    }

    [Fact]
    public async Task Database_Backup_Can_Be_Reopened()
    {
        var repository = await CreateAsync();
        await repository.AddAsync(NewItem("back me up", pinned: true));
        var backupPath = Path.Combine(_directory, "backup.db");
        await repository.CreateDatabaseBackupAsync(backupPath);

        var restored = new ClipboardRepository(backupPath);
        await restored.InitializeAsync();
        Assert.Single(await restored.SearchAsync("back me up", pinned: true));
    }

    private async Task<ClipboardRepository> CreateAsync()
    {
        var repository = new ClipboardRepository(Path.Combine(_directory, "test.db"));
        await repository.InitializeAsync();
        return repository;
    }

    private static ClipboardItem NewItem(string text, bool pinned = false) => new()
    {
        ContentType = ClipboardContentType.Text, Preview = text, TextContent = text,
        ContentHash = ContentUtilities.Sha256(text), IsPinned = pinned
    };

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
