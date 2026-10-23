using Trekky.Modules.Identity.Contracts;
using Trekky.Modules.Identity.Domain;

namespace Trekky.Modules.Identity.Tests;

public sealed class SignUpValidationTests
{
    private const string ValidPassword = "correct horse battery";

    [Fact]
    public void Valid_request_is_trimmed_and_gets_a_default_display_name()
    {
        var ok = SignUpValidation.TryValidate(
            new SignUpRequest("  ada@example.com ", ValidPassword, "  Analytical Engines  "),
            out var input,
            out var errors);

        Assert.True(ok);
        Assert.Empty(errors);
        Assert.NotNull(input);
        Assert.Equal("ada@example.com", input.Email);
        Assert.Equal("Analytical Engines", input.OrganizationName);
        Assert.Equal("ada", input.DisplayName);
    }

    [Fact]
    public void Given_display_name_is_kept()
    {
        SignUpValidation.TryValidate(
            new SignUpRequest("ada@example.com", ValidPassword, "Org", " Ada Lovelace "), out var input, out _);

        Assert.Equal("Ada Lovelace", input?.DisplayName);
    }

    [Fact]
    public void Missing_fields_are_all_reported()
    {
        var ok = SignUpValidation.TryValidate(new SignUpRequest(null, null, "   "), out var input, out var errors);

        Assert.False(ok);
        Assert.Null(input);
        Assert.Equal(new[] { "email", "organizationName", "password" }, errors.Keys.Order());
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("Ada <ada@example.com>")]
    [InlineData("ada@example.com, bob@example.com")]
    public void Invalid_email_is_rejected(string email)
    {
        var ok = SignUpValidation.TryValidate(new SignUpRequest(email, ValidPassword, "Org"), out _, out var errors);

        Assert.False(ok);
        Assert.Equal("Enter a valid email address.", Assert.Single(errors["email"]));
    }

    [Fact]
    public void Email_longer_than_254_characters_is_rejected()
    {
        var email = new string('a', 245) + "@example.com";

        Assert.False(SignUpValidation.IsValidEmail(email));
    }

    [Theory]
    [InlineData(11, false)]
    [InlineData(12, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Password_must_be_12_to_128_characters(int length, bool valid)
    {
        var ok = SignUpValidation.TryValidate(
            new SignUpRequest("ada@example.com", new string('x', length), "Org"), out _, out var errors);

        Assert.Equal(valid, ok);
        Assert.Equal(valid, !errors.ContainsKey("password"));
    }

    [Theory]
    [InlineData(80, true)]
    [InlineData(81, false)]
    public void Organization_name_is_at_most_80_characters(int length, bool valid)
    {
        var ok = SignUpValidation.TryValidate(
            new SignUpRequest("ada@example.com", ValidPassword, new string('o', length)), out _, out _);

        Assert.Equal(valid, ok);
    }

    [Fact]
    public void Default_display_name_is_cut_to_80_characters()
    {
        var email = new string('a', 100) + "@example.com";

        Assert.Equal(80, SignUpValidation.DefaultDisplayName(email).Length);
    }

    [Fact]
    public void Login_requires_email_and_password()
    {
        Assert.False(SignUpValidation.TryValidate(new LoginRequest(" ", ""), out var errors));
        Assert.Equal(new[] { "email", "password" }, errors.Keys.Order());
        Assert.True(SignUpValidation.TryValidate(new LoginRequest("ada@example.com", "x"), out _));
    }
}
