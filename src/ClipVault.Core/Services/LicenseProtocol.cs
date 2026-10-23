using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipVault.Core.Services;

public sealed record LicenseApiRequest(
    [property: JsonPropertyName("operation")] string Operation,
    [property: JsonPropertyName("licenseKey")] string LicenseKey,
    [property: JsonPropertyName("hwidHash")] string HwidHash,
    [property: JsonPropertyName("appVersion")] string AppVersion);

public sealed record OfflineTokenResult(bool Valid, string Code, DateTimeOffset? ExpiresAt = null);

public static class OfflineLicenseTokenValidator
{
    public static OfflineTokenResult Validate(string token, string publicKeyPem, string expectedHwid, DateTimeOffset now)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return new(false, "invalid_token");
        try
        {
            var signed = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
            using var key = ECDsa.Create();
            key.ImportFromPem(publicKeyPem);
            if (!key.VerifyData(signed, Decode(parts[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                return new(false, "invalid_token");
            using var payload = JsonDocument.Parse(Decode(parts[1]));
            var root = payload.RootElement;
            if (root.GetProperty("v").GetInt32() != 1 || root.GetProperty("hwidHash").GetString() != expectedHwid)
                return new(false, "device_mismatch");
            var expiry = DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("exp").GetInt64());
            return now <= expiry ? new(true, "offline_grace", expiry) : new(false, "offline_expired", expiry);
        }
        catch (FormatException) { return new(false, "invalid_token"); }
        catch (JsonException) { return new(false, "invalid_token"); }
        catch (CryptographicException) { return new(false, "invalid_token"); }
        catch (KeyNotFoundException) { return new(false, "invalid_token"); }
    }

    private static byte[] Decode(string value)
    {
        value = value.Replace('-', '+').Replace('_', '/');
        value += new string('=', (4 - value.Length % 4) % 4);
        return Convert.FromBase64String(value);
    }
}
