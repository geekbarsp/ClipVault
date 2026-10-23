using System.Text.Json;
using System.Text.Json.Serialization;
using ClipVault.Core.Models;

namespace ClipVault.Core.Services;

public sealed class SettingsStore(string path)
{
    private readonly ClipVaultJsonContext _jsonContext = new(new JsonSerializerOptions { WriteIndented = true });

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return new AppSettings();
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync(stream, _jsonContext.AppSettings, cancellationToken) ?? new AppSettings();
        }
        catch (JsonException) { return new AppSettings(); }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        await using (var stream = File.Create(temporary))
            await JsonSerializer.SerializeAsync(stream, settings, _jsonContext.AppSettings, cancellationToken);
        File.Move(temporary, path, true);
    }
}

[JsonSerializable(typeof(AppSettings))]
internal sealed partial class ClipVaultJsonContext : JsonSerializerContext;
