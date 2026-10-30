using Trekky.Modules.Projects.Application.Contracts;
using Trekky.Modules.Projects.Application.Projects;

namespace Trekky.Modules.Projects.Tests;

public sealed class CreateProjectRequestValidatorTests
{
    private readonly CreateProjectRequestValidator _validator = new();

    [Theory]
    [InlineData("Trekky", "TRK")]
    [InlineData("A", "AB")]
    [InlineData("Website 2", "WEB2")]
    [InlineData("Ten chars", "ABCDEFGHIJ")]
    public void Accepts_a_name_and_a_valid_key(string name, string key)
        => Assert.True(_validator.Validate(new CreateProjectRequest(name, key)).IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Requires_a_name(string? name)
        => AssertInvalid(new CreateProjectRequest(name, "TRK"), "name");

    [Fact]
    public void Rejects_a_name_with_a_null_character()
        => AssertInvalid(new CreateProjectRequest("Tre\0kky", "TRK"), "name");

    [Fact]
    public void Limits_the_trimmed_name_to_80_characters()
    {
        Assert.True(_validator.Validate(new CreateProjectRequest($"  {new string('a', 80)}  ", "TRK")).IsValid);
        AssertInvalid(new CreateProjectRequest(new string('a', 81), "TRK"), "name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("T")]
    [InlineData("trk")]
    [InlineData("1TRK")]
    [InlineData("TR-K")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("TRK\n")]
    public void Rejects_a_key_that_does_not_match_the_pattern(string? key)
        => AssertInvalid(new CreateProjectRequest("Trekky", key), "key");

    private void AssertInvalid(CreateProjectRequest request, string field)
    {
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == field);
    }
}
