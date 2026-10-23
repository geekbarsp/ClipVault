using System.IO.Compression;
using ClipVault.Core.Services;

namespace ClipVault_App.Services;

public sealed class BackupService(ClipboardRepository repository)
{
    private static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipVault");

    public async Task CreateAsync(string destinationPath)
    {
        var temporary = Path.Combine(Path.GetTempPath(), "ClipVaultBackup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            await repository.CreateDatabaseBackupAsync(Path.Combine(temporary, "clipvault.db"));
            var settings = Path.Combine(DataDirectory, "settings.json");
            if (File.Exists(settings)) File.Copy(settings, Path.Combine(temporary, "settings.json"), true);
            var images = Path.Combine(DataDirectory, "Images");
            if (Directory.Exists(images)) CopyDirectory(images, Path.Combine(temporary, "Images"));
            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            ZipFile.CreateFromDirectory(temporary, destinationPath, CompressionLevel.Optimal, false);
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    public async Task RestoreAsync(string sourcePath)
    {
        var temporary = Path.Combine(Path.GetTempPath(), "ClipVaultRestore", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            ZipFile.ExtractToDirectory(sourcePath, temporary);
            var database = Path.Combine(temporary, "clipvault.db");
            if (!File.Exists(database)) throw new InvalidDataException("This is not a valid ClipVault backup.");
            Directory.CreateDirectory(DataDirectory);
            var liveDatabase = Path.Combine(DataDirectory, "clipvault.db");
            foreach (var sidecar in new[] { liveDatabase + "-wal", liveDatabase + "-shm" }) if (File.Exists(sidecar)) File.Delete(sidecar);
            File.Copy(database, liveDatabase, true);
            var settings = Path.Combine(temporary, "settings.json");
            if (File.Exists(settings)) File.Copy(settings, Path.Combine(DataDirectory, "settings.json"), true);
            var images = Path.Combine(temporary, "Images");
            if (Directory.Exists(images)) CopyDirectory(images, Path.Combine(DataDirectory, "Images"));
            await repository.InitializeAsync();
            await repository.RelinkImagePathsAsync(Path.Combine(DataDirectory, "Images"));
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var directory in Directory.EnumerateDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
