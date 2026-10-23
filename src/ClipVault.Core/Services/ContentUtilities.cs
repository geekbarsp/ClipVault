using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClipVault.Core.Services;

public static partial class ContentUtilities
{
    public static string Sha256(ReadOnlySpan<byte> content) => Convert.ToHexString(SHA256.HashData(content));
    public static string Sha256(string content) => Sha256(Encoding.UTF8.GetBytes(content));

    public static bool IsUrl(string text) => Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static string Preview(string text, int maxLength = 240)
    {
        var normalized = Whitespace().Replace(text.Trim(), " ");
        return normalized.Length <= maxLength ? normalized : normalized[..(maxLength - 1)] + "…";
    }

    public static string RemoveDuplicateLines(string value) => string.Join(Environment.NewLine,
        value.ReplaceLineEndings("\n").Split('\n').Distinct(StringComparer.Ordinal));
    public static string RemoveBlankLines(string value) => string.Join(Environment.NewLine,
        value.ReplaceLineEndings("\n").Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)));
    public static string SortLines(string value) => string.Join(Environment.NewLine,
        value.ReplaceLineEndings("\n").Split('\n').Order(StringComparer.CurrentCultureIgnoreCase));
    public static string FormatJson(string value) => JsonSerializer.Serialize(JsonDocument.Parse(value), new JsonSerializerOptions { WriteIndented = true });
    public static string MinifyJson(string value) => JsonSerializer.Serialize(JsonDocument.Parse(value));
    public static string Base64Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    public static string Base64Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
    public static (int Words, int Characters, int Lines) Count(string value) =>
        (Word().Matches(value).Count, value.Length, value.Length == 0 ? 0 : value.Count(c => c == '\n') + 1);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
    [GeneratedRegex(@"\S+")]
    private static partial Regex Word();
}
