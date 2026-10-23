using System.Text.Json;
using System.Text.Json.Serialization;
using ClipVault.Core.Models;
using Microsoft.Data.Sqlite;

namespace ClipVault.Core.Services;

public sealed class SettingsStore(string databasePath, string? legacyJsonPath = null)
{
    private readonly ClipVaultJsonContext _jsonContext = new(new JsonSerializerOptions { WriteIndented = true });
    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString();

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Json FROM AppSettings WHERE Id=1";
        var json = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try { return JsonSerializer.Deserialize(json, _jsonContext.AppSettings) ?? new AppSettings(); }
            catch (JsonException) { }
        }

        var legacy = legacyJsonPath ?? (Path.GetExtension(databasePath).Equals(".json", StringComparison.OrdinalIgnoreCase) ? databasePath : null);
        if (legacy is null || !File.Exists(legacy)) return new AppSettings();
        try
        {
            await using var stream = File.OpenRead(legacy);
            var migrated = await JsonSerializer.DeserializeAsync(stream, _jsonContext.AppSettings, cancellationToken) ?? new AppSettings();
            await SaveAsync(migrated, cancellationToken);
            return migrated;
        }
        catch (JsonException) { return new AppSettings(); }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var json = JsonSerializer.Serialize(settings, _jsonContext.AppSettings);
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO AppSettings(Id,Json,UpdatedAt) VALUES(1,$json,$updated) ON CONFLICT(Id) DO UPDATE SET Json=excluded.Json, UpdatedAt=excluded.UpdatedAt";
        command.Parameters.AddWithValue("$json", json);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS AppSettings (Id INTEGER PRIMARY KEY CHECK(Id=1), Json TEXT NOT NULL, UpdatedAt TEXT NOT NULL);";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

[JsonSerializable(typeof(AppSettings))]
internal sealed partial class ClipVaultJsonContext : JsonSerializerContext;
