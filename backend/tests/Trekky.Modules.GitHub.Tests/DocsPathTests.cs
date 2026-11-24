using Trekky.Modules.GitHub.Domain;

namespace Trekky.Modules.GitHub.Tests;

public sealed class DocsPathTests
{
    [Theory]
    [InlineData("docs", "docs/")]
    [InlineData("docs/", "docs/")]
    [InlineData("/docs", "docs/")]
    [InlineData("  /docs/  ", "docs/")]
    [InlineData("handbook/en", "handbook/en/")]
    [InlineData("", "")]
    [InlineData("/", "")]
    [InlineData(".github/docs", ".github/docs/")]
    [InlineData("docs v2", "docs v2/")]
    public void A_folder_is_kept_without_a_leading_slash_and_with_a_trailing_one(string input, string expected)
        => Assert.Equal(expected, DocsPath.Normalize(input));

    [Theory]
    [InlineData("..")]
    [InlineData("docs/../secrets")]
    [InlineData("./docs")]
    [InlineData("docs//api")]
    [InlineData("docs\\api")]
    [InlineData("docs/ api")]
    [InlineData("do\ncs")]
    public void A_path_that_could_leave_the_folder_or_confuse_GitHub_is_refused(string input)
        => Assert.Null(DocsPath.Normalize(input));

    [Fact]
    public void A_path_longer_than_the_limit_is_refused()
        => Assert.Null(DocsPath.Normalize(new string('a', DocsPath.MaxLength + 1)));
}
