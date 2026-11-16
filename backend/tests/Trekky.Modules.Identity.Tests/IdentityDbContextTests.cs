using Trekky.Modules.Identity.Persistence;

namespace Trekky.Modules.Identity.Tests;

public sealed class IdentityDbContextTests
{
    [Theory]
    [InlineData("Users", "users")]
    [InlineData("UserClaims", "user_claims")]
    [InlineData("Authorizations", "authorizations")]
    public void Framework_table_names_become_snake_case(string name, string expected)
        => Assert.Equal(expected, IdentityDbContext.ToSnakeCase(name));
}
