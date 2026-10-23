using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClipVault.Core.Services;

namespace ClipVault.Tests;

public sealed class LicenseProtocolTests
{
    [Fact]
    public void Request_Contains_Only_Licensing_Metadata()
    {
        var json = JsonSerializer.Serialize(new LicenseApiRequest("validate", "CLIP-AAAA-BBBB-CCCC-DDDD", new string('a', 64), "1.0.0"));
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["appVersion", "hwidHash", "licenseKey", "operation"], document.RootElement.EnumerateObject().Select(x => x.Name).Order().ToArray());
        Assert.DoesNotContain("clipboard", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("content", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("image", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Offline_Token_Enforces_Signature_Device_And_Expiry()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = signingKey.ExportSubjectPublicKeyInfoPem();
        var now = DateTimeOffset.UtcNow;
        var token = Sign(signingKey, new { v = 1, hwidHash = "device-a", exp = now.AddDays(7).ToUnixTimeSeconds() });
        Assert.True(OfflineLicenseTokenValidator.Validate(token, publicKey, "device-a", now).Valid);
        Assert.Equal("device_mismatch", OfflineLicenseTokenValidator.Validate(token, publicKey, "device-b", now).Code);
        Assert.Equal("offline_expired", OfflineLicenseTokenValidator.Validate(token, publicKey, "device-a", now.AddDays(8)).Code);
        Assert.Equal("invalid_token", OfflineLicenseTokenValidator.Validate(token + "x", publicKey, "device-a", now).Code);
    }

    private static string Sign(ECDsa key, object payload)
    {
        static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64(Encoding.UTF8.GetBytes("{\"alg\":\"ES256\",\"typ\":\"CVLT\"}"));
        var body = B64(JsonSerializer.SerializeToUtf8Bytes(payload));
        var data = Encoding.UTF8.GetBytes($"{header}.{body}");
        var signature = key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{header}.{body}.{B64(signature)}";
    }
}
