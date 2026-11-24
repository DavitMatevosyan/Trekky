using Trekky.Modules.GitHub.Application.GitHub;

namespace Trekky.Modules.GitHub.Tests;

public sealed class GitHubUrlsTests
{
    [Fact]
    public void An_organization_installation_is_managed_under_the_organization()
        => Assert.Equal(
            "https://github.com/organizations/acme/settings/installations/42",
            GitHubUrls.Installation(42, "acme", "Organization"));

    [Fact]
    public void A_user_installation_is_managed_in_the_user_settings()
        => Assert.Equal("https://github.com/settings/installations/42", GitHubUrls.Installation(42, "davit", "User"));

    [Theory]
    [InlineData("dev", "docs/", "https://github.com/acme/handbook/tree/dev/docs")]
    [InlineData("release/1.0", "guides/en/", "https://github.com/acme/handbook/tree/release/1.0/guides/en")]
    [InlineData("main", "", "https://github.com/acme/handbook/tree/main")]
    [InlineData("dev", "docs v2/", "https://github.com/acme/handbook/tree/dev/docs%20v2")]
    [InlineData("feat#1", "docs/", "https://github.com/acme/handbook/tree/feat%231/docs")]
    public void The_docs_folder_link_escapes_each_segment(string branch, string docsPath, string expected)
        => Assert.Equal(expected, GitHubUrls.Folder("acme/handbook", branch, docsPath));
}
