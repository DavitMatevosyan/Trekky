using Trekky.Modules.Identity.Domain;

namespace Trekky.Modules.Identity.Tests;

public sealed class InviteTokenTests
{
    [Fact]
    public void New_tokens_are_43_url_safe_characters_and_never_repeat()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => InviteToken.New()).ToList();

        Assert.All(tokens, token => Assert.True(InviteToken.IsWellFormed(token), token));
        Assert.Equal(tokens.Count, tokens.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_hash_is_sha256_of_the_token()
    {
        var token = InviteToken.New();

        Assert.Equal(32, InviteToken.Hash(token).Length);
        Assert.Equal(InviteToken.Hash(token), InviteToken.Hash(token));
        Assert.NotEqual(InviteToken.Hash(token), InviteToken.Hash(InviteToken.New()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // 42
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // 44
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+")] // base64, not base64url
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void Anything_else_is_not_a_token(string? token)
        => Assert.False(InviteToken.IsWellFormed(token));
}
