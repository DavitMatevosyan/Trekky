using FluentValidation.Results;
using Trekky.Modules.Identity.Application.Accounts;
using Trekky.Modules.Identity.Application.Contracts;

namespace Trekky.Modules.Identity.Tests;

public sealed class SignUpRequestValidatorTests
{
    private const string ValidPassword = "correct horse battery";

    private readonly SignUpRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.Validate(new SignUpRequest("  ada@example.com ", ValidPassword, "  Analytical Engines  "));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Missing_fields_are_all_reported_by_their_json_names()
    {
        var result = _validator.Validate(new SignUpRequest(null, null, "   "));

        Assert.False(result.IsValid);
        Assert.Equal(["email", "organizationName", "password"], Errors(result).Keys.Order());
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("Ada <ada@example.com>")]
    [InlineData("ada@example.com, bob@example.com")]
    public void Invalid_email_is_rejected(string email)
    {
        var result = _validator.Validate(new SignUpRequest(email, ValidPassword, "Org"));

        Assert.Equal("Enter a valid email address.", Assert.Single(Errors(result)["email"]));
    }

    [Fact]
    public void Email_longer_than_254_characters_is_rejected()
    {
        var email = new string('a', 245) + "@example.com";

        Assert.False(SignUpRequestValidator.IsValidEmail(email));
    }

    [Theory]
    [InlineData(11, false)]
    [InlineData(12, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Password_must_be_12_to_128_characters(int length, bool valid)
    {
        var result = _validator.Validate(new SignUpRequest("ada@example.com", new string('x', length), "Org"));

        Assert.Equal(valid, result.IsValid);
        Assert.Equal(valid, !Errors(result).ContainsKey("password"));
    }

    [Theory]
    [InlineData(80, true)]
    [InlineData(81, false)]
    public void Organization_name_is_at_most_80_characters(int length, bool valid)
    {
        var result = _validator.Validate(new SignUpRequest("ada@example.com", ValidPassword, new string('o', length)));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(80, true)]
    [InlineData(81, false)]
    public void Display_name_is_at_most_80_characters(int length, bool valid)
    {
        var result = _validator.Validate(new SignUpRequest("ada@example.com", ValidPassword, "Org", new string('d', length)));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Each_field_reports_only_its_first_error()
    {
        var result = _validator.Validate(new SignUpRequest(" ", ValidPassword, "Org"));

        Assert.Equal("Email is required.", Assert.Single(Errors(result)["email"]));
    }

    private static IDictionary<string, string[]> Errors(ValidationResult result) => result.ToDictionary();
}
