using System.Text;
using Trekky.Modules.GitHub.Infrastructure;

namespace Trekky.Modules.GitHub.Tests;

public sealed class DocsSearchTextTests
{
    [Theory]
    [InlineData("docker compose", "docker:* & compose:*")]
    [InlineData("Dock  comp dock", "dock:* & comp:*")]
    [InlineData("postgres:* & !(') | mail", "postgres:* & mail:*")]
    [InlineData("TREK_75 v1.1", "trek_75:* & v1:* & 1:*")]
    [InlineData("Größe straße", "größe:* & straße:*")]
    public void A_query_becomes_a_prefix_match_of_every_word(string query, string expected)
        => Assert.Equal(expected, DocsService.ToPrefixQuery(query));

    [Theory]
    [InlineData("")]
    [InlineData("&|!():*")]
    [InlineData("  ' \" ")]
    public void A_query_without_words_has_no_tsquery(string query)
        => Assert.Null(DocsService.ToPrefixQuery(query));

    [Fact]
    public void At_most_ten_words_are_used()
    {
        var query = string.Join(' ', Enumerable.Range(1, 15).Select(i => $"w{i}"));

        Assert.Equal(DocsService.MaxSearchTerms, DocsService.ToPrefixQuery(query)!.Split(" & ").Length);
    }

    [Fact]
    public void A_headline_is_split_into_plain_and_matched_runs_with_whitespace_collapsed()
    {
        var parts = DocsService.ToSnippet("Run \uE000Docker\uE001 \uE000Compose\uE001\n\n  to start");

        Assert.Equal(
            [new("Run ", false), new("Docker", true), new(" ", false), new("Compose", true), new(" to start", false)],
            parts);
    }

    [Fact]
    public void A_headline_without_matches_is_one_plain_run()
        => Assert.Equal([new("Just text", false)], DocsService.ToSnippet("  Just\ttext "));

    [Fact]
    public void File_text_is_decoded_without_a_byte_order_mark_or_NUL_characters()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("# Title\0 é")];

        Assert.Equal("# Title é", DocsService.Decode(bytes));
    }
}
