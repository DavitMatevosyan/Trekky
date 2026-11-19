using Trekky.Modules.Identity.Domain;
using Trekky.SharedKernel;

namespace Trekky.Modules.Identity.Tests;

/// <summary>TREK-46: the roles Identity stores and puts in the token are the ones the role policies check.</summary>
public sealed class RolesTests
{
    [Fact]
    public void Stored_roles_are_the_roles_the_policies_know()
        => Assert.Equal([Roles.Owner, Roles.Admin, Roles.Member, Roles.Viewer], TrekkyRoles.All);
}
