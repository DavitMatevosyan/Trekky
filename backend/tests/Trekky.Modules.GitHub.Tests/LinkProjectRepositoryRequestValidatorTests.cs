using Trekky.Modules.GitHub.Application.Contracts;
using Trekky.Modules.GitHub.Application.Repositories;

namespace Trekky.Modules.GitHub.Tests;

public sealed class LinkProjectRepositoryRequestValidatorTests
{
    private readonly LinkProjectRepositoryRequestValidator _validator = new();

    [Fact]
    public void A_connection_and_repository_are_enough_the_rest_has_defaults()
        => Assert.True(_validator.Validate(new LinkProjectRepositoryRequest(Guid.CreateVersion7(), "acme/handbook", null, null)).IsValid);

    [Theory]
    [InlineData("acme/handbook")]
    [InlineData("a/b")]
    [InlineData("Acme-Corp/docs.site_v2")]
    public void Repository_names_GitHub_allows_pass(string repository)
        => Assert.True(_validator.Validate(new LinkProjectRepositoryRequest(Guid.CreateVersion7(), repository, "docs", "dev")).IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("handbook")]
    [InlineData("acme/handbook/extra")]
    [InlineData("-acme/handbook")]
    [InlineData("acme/hand book")]
    [InlineData("acme/handbook\n")]
    public void A_repository_that_is_not_owner_slash_name_fails(string? repository)
        => AssertFailsOn("repository", new LinkProjectRepositoryRequest(Guid.CreateVersion7(), repository, null, null));

    [Fact]
    public void A_missing_connection_fails()
    {
        AssertFailsOn("installationId", new LinkProjectRepositoryRequest(null, "acme/handbook", null, null));
        AssertFailsOn("installationId", new LinkProjectRepositoryRequest(Guid.Empty, "acme/handbook", null, null));
    }

    [Fact]
    public void A_docs_path_leaving_the_repository_fails()
        => AssertFailsOn("docsPath", new LinkProjectRepositoryRequest(Guid.CreateVersion7(), "acme/handbook", "../x", null));

    [Fact]
    public void An_invalid_branch_fails()
        => AssertFailsOn("branch", new LinkProjectRepositoryRequest(Guid.CreateVersion7(), "acme/handbook", null, "a..b"));

    private void AssertFailsOn(string field, LinkProjectRepositoryRequest request)
    {
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Equal([field], result.Errors.Select(e => e.PropertyName).Distinct());
    }
}
