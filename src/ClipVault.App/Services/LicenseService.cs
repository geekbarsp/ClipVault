using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClipVault.Core.Services;
using Microsoft.Win32;

namespace ClipVault_App.Services;

public sealed record LicenseCheck(bool Allowed, string Code, string Message, DateTimeOffset? ExpiresAt = null);
internal sealed record StoredLicense(string LicenseKey, string Token, DateTimeOffset LastServerTime);
internal sealed record LicenseResponse(bool Ok, string? Status, string? Token, DateTimeOffset? ServerTime, DateTimeOffset? ExpiresAt, DateTimeOffset? LicenseExpiresAt, string? Code, string? Message);

public sealed class LicenseService(HttpClient httpClient)
{
    internal const string Endpoint = "https://dzvliwawrakacvgvxarz.supabase.co/functions/v1/license";
    internal const string PublishableKey = "sb_publishable_PD_M14OPyanpcQD2EabPbw_dJktIyVD";
    private const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEs0a+Lxc3c2PNL+gDzmYyLTUykt7R
        FDQw9l2WdaXiUTNFfE+MZNajRRlooSpOdf/AHPcSg00xDRrZL9LnqwdLtQ==
        -----END PUBLIC KEY-----
        """;
    private readonly string _credentialPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipVault", "license.dat");

    public async Task<LicenseCheck> CheckStartupAsync(CancellationToken cancellationToken = default)
    {
        var stored = LoadCredential();
        if (stored is null) return new(false, "activation_required", "Enter a license key to activate ClipVault.");
        try { return await ValidateOnlineAsync(stored.LicenseKey, cancellationToken); }
        catch (HttpRequestException) { return ValidateOffline(stored); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return ValidateOffline(stored); }
    }

    public async Task<LicenseCheck> ActivateAsync(string licenseKey, CancellationToken cancellationToken = default) =>
        await SendAsync("activate", licenseKey, cancellationToken);

    private async Task<LicenseCheck> ValidateOnlineAsync(string licenseKey, CancellationToken cancellationToken) =>
        await SendAsync("validate", licenseKey, cancellationToken);

    private async Task<LicenseCheck> SendAsync(string operation, string licenseKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("apikey", PublishableKey);
        request.Content = JsonContent.Create(new LicenseApiRequest(operation, NormalizeKey(licenseKey), DeviceHash(), Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<LicenseResponse>(cancellationToken: cancellationToken);
        if (result?.Ok == true && result.Token is not null && result.ServerTime is not null)
        {
            SaveCredential(new StoredLicense(NormalizeKey(licenseKey), result.Token, result.ServerTime.Value));
            return new(true, "active", "License active.", result.LicenseExpiresAt ?? result.ExpiresAt);
        }
        return new(false, result?.Code ?? "service_error", result?.Message ?? "The license could not be validated.");
    }

    private LicenseCheck ValidateOffline(StoredLicense stored)
    {
        if (DateTimeOffset.UtcNow < stored.LastServerTime.AddMinutes(-5)) return new(false, "clock_changed", "The system clock changed. Connect to the internet to validate again.");
        var validation = OfflineLicenseTokenValidator.Validate(stored.Token, PublicKeyPem, DeviceHash(), DateTimeOffset.UtcNow);
        return validation.Code switch
        {
            "offline_grace" => new(true, validation.Code, $"Offline license active until {validation.ExpiresAt?.LocalDateTime:g}.", validation.ExpiresAt),
            "offline_expired" => new(false, validation.Code, "Offline access expired. Connect to the internet to validate your license.", validation.ExpiresAt),
            "device_mismatch" => new(false, validation.Code, "This activation belongs to another Windows installation."),
            _ => new(false, validation.Code, "The saved activation is invalid.")
        };
    }

    internal static string NormalizeKey(string value) => value.Trim().ToUpperInvariant().Replace(" ", "");
    internal static string DeviceHash()
    {
        var machineGuid = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null)?.ToString();
        if (string.IsNullOrWhiteSpace(machineGuid)) machineGuid = Environment.MachineName;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"ClipVault.HWID.v1|{machineGuid.Trim().ToUpperInvariant()}"))).ToLowerInvariant();
    }

    private StoredLicense? LoadCredential()
    {
        try
        {
            if (!File.Exists(_credentialPath)) return null;
            var clear = ProtectedData.Unprotect(File.ReadAllBytes(_credentialPath), Encoding.UTF8.GetBytes("ClipVault.License.v1"), DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<StoredLicense>(clear);
        }
        catch (CryptographicException) { return null; }
        catch (JsonException) { return null; }
    }

    private void SaveCredential(StoredLicense value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_credentialPath)!);
        var clear = JsonSerializer.SerializeToUtf8Bytes(value);
        var encrypted = ProtectedData.Protect(clear, Encoding.UTF8.GetBytes("ClipVault.License.v1"), DataProtectionScope.CurrentUser);
        var temporary = _credentialPath + ".tmp";
        File.WriteAllBytes(temporary, encrypted);
        File.Move(temporary, _credentialPath, true);
    }

}
