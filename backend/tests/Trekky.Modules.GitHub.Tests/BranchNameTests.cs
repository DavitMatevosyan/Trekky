using Trekky.Modules.GitHub.Domain;

namespace Trekky.Modules.GitHub.Tests;

public sealed class BranchNameTests
{
    [Theory]
    [InlineData("dev")]
    [InlineData("main")]
    [InlineData("release/1.0")]
    [InlineData("TREK-74-github-app-connection")]
    [InlineData("feature/docs_v2")]
    public void Git_branch_names_are_accepted(string name) => Assert.True(BranchName.IsValid(name));

    [Theory]
    [InlineData("")]
    [InlineData("@")]
    [InlineData("has space")]
    [InlineData("a..b")]
    [InlineData("a~b")]
    [InlineData("a^b")]
    [InlineData("a:b")]
    [InlineData("a?b")]
    [InlineData("a*b")]
    [InlineData("a[b")]
    [InlineData("a\\b")]
    [InlineData("a@{b")]
    [InlineData("/lead")]
    [InlineData("trail/")]
    [InlineData("a//b")]
    [InlineData("-dash")]
    [InlineData("dot.")]
    [InlineData("name.lock")]
    [InlineData("feature/.hidden")]
    [InlineData("tab\there")]
    public void Names_git_refuses_are_refused(string name) => Assert.False(BranchName.IsValid(name));

    [Fact]
    public void A_name_longer_than_255_characters_is_refused()
        => Assert.False(BranchName.IsValid(new string('b', BranchName.MaxLength + 1)));
}
