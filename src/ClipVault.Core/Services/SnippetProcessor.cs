using System.Text.RegularExpressions;

namespace ClipVault.Core.Services;

public static partial class SnippetProcessor
{
    public static IReadOnlyList<string> GetPlaceholders(string template) => Placeholder().Matches(template)
        .Select(m => m.Groups[1].Value)
        .Where(name => !name.Equals("date", StringComparison.OrdinalIgnoreCase) && !name.Equals("time", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static string Expand(string template, IReadOnlyDictionary<string, string>? values = null, DateTimeOffset? now = null)
    {
        var instant = now ?? DateTimeOffset.Now;
        return Placeholder().Replace(template, match => match.Groups[1].Value.ToLowerInvariant() switch
        {
            "date" => instant.ToString("d"),
            "time" => instant.ToString("t"),
            var name when values is not null && values.TryGetValue(name, out var value) => value,
            _ => match.Value
        });
    }

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9_-]*)\}")]
    private static partial Regex Placeholder();
}
