using Trekky.Modules.Identity.Application.Accounts;
using Trekky.Modules.Identity.Application.Contracts;

namespace Trekky.Modules.Identity.Tests;

public sealed class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Login_requires_email_and_password()
    {
        var result = _validator.Validate(new LoginRequest(" ", ""));

        Assert.False(result.IsValid);
        Assert.Equal(["email", "password"], result.ToDictionary().Keys.Order());
    }

    [Fact]
    public void Any_non_empty_credentials_pass_validation()
        => Assert.True(_validator.Validate(new LoginRequest("ada@example.com", "x")).IsValid);
}
