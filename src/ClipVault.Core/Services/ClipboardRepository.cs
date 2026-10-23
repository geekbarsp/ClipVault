using ClipVault.Core.Models;
using Microsoft.Data.Sqlite;

namespace ClipVault.Core.Services;

public sealed class ClipboardRepository(string databasePath)
{
    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;
            CREATE TABLE IF NOT EXISTS SchemaMigrations (Version INTEGER PRIMARY KEY, AppliedAt TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS ClipboardItems (
              Id TEXT PRIMARY KEY, ContentType INTEGER NOT NULL, Preview TEXT NOT NULL, Title TEXT, TextContent TEXT,
              BinaryPath TEXT, SourceApplication TEXT, ContentHash TEXT NOT NULL, CreatedAt TEXT NOT NULL,
              UpdatedAt TEXT NOT NULL, IsPinned INTEGER NOT NULL DEFAULT 0, IsFavorite INTEGER NOT NULL DEFAULT 0,
              IsDeleted INTEGER NOT NULL DEFAULT 0, UsageCount INTEGER NOT NULL DEFAULT 0, DuplicateCount INTEGER NOT NULL DEFAULT 1,
              OcrText TEXT, Notes TEXT, CollectionName TEXT);
            CREATE UNIQUE INDEX IF NOT EXISTS IX_ClipboardItems_Hash_Active ON ClipboardItems(ContentHash) WHERE IsDeleted=0;
            CREATE INDEX IF NOT EXISTS IX_ClipboardItems_CreatedAt ON ClipboardItems(CreatedAt DESC);
            CREATE INDEX IF NOT EXISTS IX_ClipboardItems_Filters ON ClipboardItems(IsDeleted, IsPinned, IsFavorite, ContentType);
            CREATE TABLE IF NOT EXISTS Tags (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE UNIQUE);
            CREATE TABLE IF NOT EXISTS ClipboardItemTags (ItemId TEXT NOT NULL REFERENCES ClipboardItems(Id) ON DELETE CASCADE, TagId INTEGER NOT NULL REFERENCES Tags(Id) ON DELETE CASCADE, PRIMARY KEY(ItemId, TagId));
            CREATE VIRTUAL TABLE IF NOT EXISTS ClipboardItemsFts USING fts5(ItemId UNINDEXED, Preview, TextContent, Notes, tokenize='unicode61');
            CREATE TRIGGER IF NOT EXISTS ClipboardItems_ai AFTER INSERT ON ClipboardItems BEGIN INSERT INTO ClipboardItemsFts(ItemId,Preview,TextContent,Notes) VALUES(new.Id,new.Preview,new.TextContent,new.Notes); END;
            CREATE TRIGGER IF NOT EXISTS ClipboardItems_au AFTER UPDATE ON ClipboardItems BEGIN DELETE FROM ClipboardItemsFts WHERE ItemId=old.Id; INSERT INTO ClipboardItemsFts(ItemId,Preview,TextContent,Notes) VALUES(new.Id,new.Preview,new.TextContent,new.Notes); END;
            CREATE TRIGGER IF NOT EXISTS ClipboardItems_ad AFTER DELETE ON ClipboardItems BEGIN DELETE FROM ClipboardItemsFts WHERE ItemId=old.Id; END;
            INSERT OR IGNORE INTO SchemaMigrations(Version, AppliedAt) VALUES(1, strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await EnsureColumnAsync(connection, "Title", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "OcrText", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "DuplicateCount", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await using var migration = connection.CreateCommand();
        migration.CommandText = "INSERT OR IGNORE INTO SchemaMigrations(Version, AppliedAt) VALUES(3, strftime('%Y-%m-%dT%H:%M:%fZ','now'));";
        await migration.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> AddAsync(ClipboardItem item, bool ignoreDuplicate = true, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        if (ignoreDuplicate)
        {
            await using var duplicate = connection.CreateCommand();
            duplicate.CommandText = "UPDATE ClipboardItems SET DuplicateCount=DuplicateCount+1, CreatedAt=$created, UpdatedAt=$updated, SourceApplication=COALESCE($source,SourceApplication) WHERE ContentHash=$hash AND IsDeleted=0";
            duplicate.Parameters.AddWithValue("$created", item.CreatedAt.ToString("O"));
            duplicate.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
            duplicate.Parameters.AddWithValue("$source", (object?)item.SourceApplication ?? DBNull.Value);
            duplicate.Parameters.AddWithValue("$hash", item.ContentHash);
            if (await duplicate.ExecuteNonQueryAsync(cancellationToken) > 0) return true;
        }
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT {(ignoreDuplicate ? "OR IGNORE" : "")} INTO ClipboardItems
            (Id,ContentType,Preview,Title,TextContent,OcrText,BinaryPath,SourceApplication,ContentHash,CreatedAt,UpdatedAt,IsPinned,IsFavorite,IsDeleted,UsageCount,DuplicateCount,Notes,CollectionName)
            VALUES($id,$type,$preview,$title,$text,$ocr,$binary,$source,$hash,$created,$updated,$pinned,$favorite,0,$usage,$duplicates,$notes,$collection);
            """;
        Bind(command, item);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(string? query = null, ClipboardContentType? type = null, bool? pinned = null, bool? favorite = null, bool includeDeleted = false, int limit = 200, ClipboardSortOrder sort = ClipboardSortOrder.Newest, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var clauses = new List<string> { "i.IsDeleted=$deleted" };
        if (!string.IsNullOrWhiteSpace(query)) clauses.Add("(i.Id IN (SELECT ItemId FROM ClipboardItemsFts WHERE ClipboardItemsFts MATCH $query) OR i.Title LIKE $textQuery OR i.OcrText LIKE $textQuery OR i.CollectionName LIKE $textQuery OR EXISTS (SELECT 1 FROM ClipboardItemTags it JOIN Tags t ON t.Id=it.TagId WHERE it.ItemId=i.Id AND t.Name LIKE $textQuery))");
        if (type is not null) clauses.Add("i.ContentType=$type");
        if (pinned is not null) clauses.Add("i.IsPinned=$pinned");
        if (favorite is not null) clauses.Add("i.IsFavorite=$favorite");
        var order = sort switch
        {
            ClipboardSortOrder.Oldest => "i.IsPinned DESC, i.CreatedAt ASC",
            ClipboardSortOrder.Alphabetical => "i.IsPinned DESC, COALESCE(NULLIF(TRIM(i.Title), ''), i.Preview) COLLATE NOCASE ASC",
            ClipboardSortOrder.MostUsed => "i.IsPinned DESC, i.UsageCount DESC, i.CreatedAt DESC",
            _ => "i.IsPinned DESC, i.CreatedAt DESC"
        };
        command.CommandText = $"SELECT i.*, COALESCE((SELECT group_concat(t.Name, char(31)) FROM ClipboardItemTags it JOIN Tags t ON t.Id=it.TagId WHERE it.ItemId=i.Id), '') AS TagList FROM ClipboardItems i WHERE {string.Join(" AND ", clauses)} ORDER BY {order} LIMIT $limit";
        command.Parameters.AddWithValue("$deleted", includeDeleted ? 1 : 0);
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 1000));
        if (!string.IsNullOrWhiteSpace(query))
        {
            command.Parameters.AddWithValue("$query", ToFtsQuery(query));
            command.Parameters.AddWithValue("$textQuery", $"%{query.Trim()}%");
        }
        if (type is not null) command.Parameters.AddWithValue("$type", (int)type.Value);
        if (pinned is not null) command.Parameters.AddWithValue("$pinned", pinned.Value ? 1 : 0);
        if (favorite is not null) command.Parameters.AddWithValue("$favorite", favorite.Value ? 1 : 0);
        var result = new List<ClipboardItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(Read(reader));
        return result;
    }

    public async Task SetPinnedAsync(Guid id, bool value, string? title = null, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ClipboardItems SET IsPinned=$value, Title=CASE WHEN $value=1 THEN $title ELSE Title END, UpdatedAt=$updated WHERE Id=$id";
        command.Parameters.AddWithValue("$value", value ? 1 : 0);
        command.Parameters.AddWithValue("$title", value && !string.IsNullOrWhiteSpace(title) ? title.Trim() : DBNull.Value);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    public Task SetFavoriteAsync(Guid id, bool value, CancellationToken ct = default) => UpdateFlagAsync(id, "IsFavorite", value, ct);
    public Task MoveToTrashAsync(Guid id, CancellationToken ct = default) => UpdateFlagAsync(id, "IsDeleted", true, ct);
    public Task RestoreAsync(Guid id, CancellationToken ct = default) => UpdateFlagAsync(id, "IsDeleted", false, ct);

    public async Task UpdateTextAsync(Guid id, string text, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ClipboardItems SET TextContent=$text, Preview=$preview, ContentHash=$hash, UpdatedAt=$updated WHERE Id=$id";
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$preview", ContentUtilities.Preview(text));
        command.Parameters.AddWithValue("$hash", ContentUtilities.Sha256(text));
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetOcrTextAsync(Guid id, string text, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ClipboardItems SET OcrText=$text, UpdatedAt=$updated WHERE Id=$id";
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetOrganizationAsync(Guid id, string? collection, IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = (SqliteTransaction)transaction;
            update.CommandText = "UPDATE ClipboardItems SET CollectionName=$collection, UpdatedAt=$updated WHERE Id=$id";
            update.Parameters.AddWithValue("$collection", string.IsNullOrWhiteSpace(collection) ? DBNull.Value : collection.Trim());
            update.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$id", id.ToString());
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = (SqliteTransaction)transaction;
            clear.CommandText = "DELETE FROM ClipboardItemTags WHERE ItemId=$id";
            clear.Parameters.AddWithValue("$id", id.ToString());
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var tag in tags.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(20))
        {
            await using var add = connection.CreateCommand();
            add.Transaction = (SqliteTransaction)transaction;
            add.CommandText = "INSERT OR IGNORE INTO Tags(Name) VALUES($tag); INSERT OR IGNORE INTO ClipboardItemTags(ItemId,TagId) SELECT $id,Id FROM Tags WHERE Name=$tag COLLATE NOCASE";
            add.Parameters.AddWithValue("$tag", tag);
            add.Parameters.AddWithValue("$id", id.ToString());
            await add.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CreateDatabaseBackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        await using var source = new SqliteConnection(ConnectionString);
        await source.OpenAsync(cancellationToken);
        await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destinationPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    public async Task RelinkImagePathsAsync(string imageDirectory, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ClipboardItems SET BinaryPath=$prefix || ContentHash || '.png' WHERE ContentType=$image AND BinaryPath IS NOT NULL";
        command.Parameters.AddWithValue("$prefix", imageDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        command.Parameters.AddWithValue("$image", (int)ClipboardContentType.Image);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CleanupAsync(int retentionDays, int maximumItems, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM ClipboardItems WHERE IsPinned=0 AND IsDeleted=0 AND datetime(CreatedAt) < datetime('now', $retention);
            DELETE FROM ClipboardItems WHERE Id IN (SELECT Id FROM ClipboardItems WHERE IsPinned=0 AND IsDeleted=0 ORDER BY CreatedAt DESC LIMIT -1 OFFSET $maximum);
            """;
        command.Parameters.AddWithValue("$retention", $"-{Math.Max(1, retentionDays)} days");
        command.Parameters.AddWithValue("$maximum", Math.Max(1, maximumItems));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpdateFlagAsync(Guid id, string column, bool value, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE ClipboardItems SET {column}=$value, UpdatedAt=$updated WHERE Id=$id";
        command.Parameters.AddWithValue("$value", value ? 1 : 0);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ToFtsQuery(string query) => string.Join(" AND ", query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(token => $"\"{token.Replace("\"", "\"\"")}\"*"));

    private static void Bind(SqliteCommand c, ClipboardItem i)
    {
        c.Parameters.AddWithValue("$id", i.Id.ToString()); c.Parameters.AddWithValue("$type", (int)i.ContentType);
        c.Parameters.AddWithValue("$preview", i.Preview); c.Parameters.AddWithValue("$title", (object?)i.Title ?? DBNull.Value); c.Parameters.AddWithValue("$text", (object?)i.TextContent ?? DBNull.Value); c.Parameters.AddWithValue("$ocr", (object?)i.OcrText ?? DBNull.Value);
        c.Parameters.AddWithValue("$binary", (object?)i.BinaryPath ?? DBNull.Value); c.Parameters.AddWithValue("$source", (object?)i.SourceApplication ?? DBNull.Value);
        c.Parameters.AddWithValue("$hash", i.ContentHash); c.Parameters.AddWithValue("$created", i.CreatedAt.ToString("O")); c.Parameters.AddWithValue("$updated", i.UpdatedAt.ToString("O"));
        c.Parameters.AddWithValue("$pinned", i.IsPinned ? 1 : 0); c.Parameters.AddWithValue("$favorite", i.IsFavorite ? 1 : 0); c.Parameters.AddWithValue("$usage", i.UsageCount); c.Parameters.AddWithValue("$duplicates", Math.Max(1, i.DuplicateCount));
        c.Parameters.AddWithValue("$notes", (object?)i.Notes ?? DBNull.Value); c.Parameters.AddWithValue("$collection", (object?)i.CollectionName ?? DBNull.Value);
    }

    private static ClipboardItem Read(SqliteDataReader r) => new()
    {
        Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))), ContentType = (ClipboardContentType)r.GetInt32(r.GetOrdinal("ContentType")),
        Preview = r.GetString(r.GetOrdinal("Preview")), Title = NullString(r, "Title"), TextContent = NullString(r, "TextContent"), OcrText = NullString(r, "OcrText"), BinaryPath = NullString(r, "BinaryPath"),
        SourceApplication = NullString(r, "SourceApplication"), ContentHash = r.GetString(r.GetOrdinal("ContentHash")),
        CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("CreatedAt"))), UpdatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("UpdatedAt"))),
        IsPinned = r.GetInt32(r.GetOrdinal("IsPinned")) != 0, IsFavorite = r.GetInt32(r.GetOrdinal("IsFavorite")) != 0,
        IsDeleted = r.GetInt32(r.GetOrdinal("IsDeleted")) != 0, UsageCount = r.GetInt64(r.GetOrdinal("UsageCount")), DuplicateCount = r.GetInt64(r.GetOrdinal("DuplicateCount")), Notes = NullString(r, "Notes"), CollectionName = NullString(r, "CollectionName"),
        Tags = r.GetString(r.GetOrdinal("TagList")).Split((char)31, StringSplitOptions.RemoveEmptyEntries)
    };

    private static async Task EnsureColumnAsync(SqliteConnection connection, string name, string definition, CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('ClipboardItems') WHERE name='{name}'";
        if (Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) != 0) return;
        await using var add = connection.CreateCommand();
        add.CommandText = $"ALTER TABLE ClipboardItems ADD COLUMN {name} {definition}";
        await add.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? NullString(SqliteDataReader r, string name) { var i = r.GetOrdinal(name); return r.IsDBNull(i) ? null : r.GetString(i); }
}
