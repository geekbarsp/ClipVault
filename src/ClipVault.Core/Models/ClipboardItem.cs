namespace ClipVault.Core.Models;

public enum ClipboardContentType { Text, Link, Html, RichText, Image, Files, Unknown }
public enum ClipboardSortOrder { Newest, Oldest, Alphabetical, MostUsed }

public sealed record ClipboardItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ClipboardContentType ContentType { get; init; }
    public required string Preview { get; init; }
    public string? Title { get; init; }
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Preview : Title;
    public string? TextContent { get; init; }
    public string? OcrText { get; init; }
    public string? BinaryPath { get; init; }
    public string? SourceApplication { get; init; }
    public required string ContentHash { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsPinned { get; init; }
    public bool IsFavorite { get; init; }
    public bool IsDeleted { get; init; }
    public long UsageCount { get; init; }
    public long DuplicateCount { get; init; } = 1;
    public string DuplicateSummary => DuplicateCount > 1 ? $"Copied {DuplicateCount} times" : string.Empty;
    public string? Notes { get; init; }
    public string? CollectionName { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string TagsSummary => string.Join(", ", Tags);
}
