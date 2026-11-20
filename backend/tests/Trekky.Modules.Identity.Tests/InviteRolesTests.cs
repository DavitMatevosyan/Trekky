using Trekky.Modules.Identity.Domain;

namespace Trekky.Modules.Identity.Tests;

/// <summary>TREK-47 with ADR-0026: Admins invite Members and Viewers; only an Owner invites Admins; nobody is invited as Owner.</summary>
public sealed class InviteRolesTests
{
    [Theory]
    [InlineData("owner", "admin", true)]
    [InlineData("owner", "member", true)]
    [InlineData("owner", "viewer", true)]
    [InlineData("owner", "owner", false)]
    [InlineData("admin", "admin", false)]
    [InlineData("admin", "member", true)]
    [InlineData("admin", "viewer", true)]
    [InlineData("member", "viewer", false)]
    [InlineData("viewer", "viewer", false)]
    [InlineData(null, "viewer", false)]
    [InlineData("owner", "boss", false)]
    public void Who_may_invite_which_role(string? inviterRole, string role, bool allowed)
        => Assert.Equal(allowed, InviteRoles.CanManage(inviterRole, role));

    [Fact]
    public void Owner_is_not_an_invitable_role()
    {
        Assert.Equal(["admin", "member", "viewer"], InviteRoles.Invitable);
        Assert.False(InviteRoles.IsInvitable("owner"));
        Assert.False(InviteRoles.IsInvitable(null));
    }
}
