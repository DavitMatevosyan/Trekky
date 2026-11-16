using Trekky.Modules.Identity.Application.Accounts;
using Trekky.Modules.Identity.Application.Contracts;

namespace Trekky.Modules.Identity.Tests;

public sealed class SignUpInputTests
{
    private const string ValidPassword = "correct horse battery";

    [Fact]
    public void Valid_request_is_trimmed_and_gets_a_default_display_name()
    {
        var input = SignUpInput.From(new SignUpRequest("  ada@example.com ", ValidPassword, "  Analytical Engines  "));

        Assert.Equal("ada@example.com", input.Email);
        Assert.Equal("Analytical Engines", input.OrganizationName);
        Assert.Equal("ada", input.DisplayName);
    }

    [Fact]
    public void Given_display_name_is_kept()
    {
        var input = SignUpInput.From(new SignUpRequest("ada@example.com", ValidPassword, "Org", " Ada Lovelace "));

        Assert.Equal("Ada Lovelace", input.DisplayName);
    }

    [Fact]
    public void Default_display_name_is_cut_to_80_characters()
    {
        var email = new string('a', 100) + "@example.com";

        Assert.Equal(80, SignUpInput.DefaultDisplayName(email).Length);
    }
}
