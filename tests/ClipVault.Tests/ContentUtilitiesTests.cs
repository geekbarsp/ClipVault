using ClipVault.Core.Services;

namespace ClipVault.Tests;

public sealed class ContentUtilitiesTests
{
    [Fact] public void Detects_Only_Http_Links() { Assert.True(ContentUtilities.IsUrl("https://example.com/a")); Assert.False(ContentUtilities.IsUrl("file:///secret")); }
    [Fact] public void Text_Tools_Are_Deterministic() { Assert.Equal("a\r\nb", ContentUtilities.RemoveDuplicateLines("a\na\nb").ReplaceLineEndings("\r\n")); Assert.Equal("{\"a\":1}", ContentUtilities.MinifyJson("{ \"a\": 1 }")); }
    [Fact] public void Counts_Text() { Assert.Equal((2, 11, 2), ContentUtilities.Count("hello\nworld")); }
    [Fact] public void Expands_Snippets_Without_Executing_Content() { var value = SnippetProcessor.Expand("Hi {name} on {date}", new Dictionary<string,string> { ["name"] = "Ada" }, new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero)); Assert.Contains("Hi Ada", value); Assert.DoesNotContain("{date}", value); }
}
