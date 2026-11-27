using Trekky.Modules.Identity.Application.Members;
using Trekky.Modules.Identity.Domain;

namespace Trekky.Modules.Identity.Tests;

/// <summary>TREK-48 with ADR-0026: Admins manage Members and Viewers; only an Owner makes or removes Admins and Owners; one Owner always stays.</summary>
public sealed class MemberRulesTests
{
    [Theory]
    [InlineData("owner", "member", "owner", true)]
    [InlineData("owner", "owner", "viewer", true)]
    [InlineData("owner", "admin", "member", true)]
    [InlineData("admin", "member", "viewer", true)]
    [InlineData("admin", "viewer", "member", true)]
    [InlineData("admin", "member", "admin", false)]
    [InlineData("admin", "admin", "member", false)]
    [InlineData("admin", "owner", "admin", false)]
    [InlineData("member", "viewer", "member", false)]
    [InlineData("viewer", "viewer", "member", false)]
    [InlineData(null, "viewer", "member", false)]
    [InlineData("owner", "member", "boss", false)]
    public void Who_may_change_which_role(string? actorRole, string currentRole, string newRole, bool allowed)
        => Assert.Equal(allowed, MemberRules.CanChangeRole(actorRole, currentRole, newRole));

    [Theory]
    [InlineData("owner", "owner", true)]
    [InlineData("owner", "admin", true)]
    [InlineData("admin", "member", true)]
    [InlineData("admin", "viewer", true)]
    [InlineData("admin", "admin", false)]
    [InlineData("admin", "owner", false)]
    [InlineData("member", "viewer", false)]
    [InlineData(null, "viewer", false)]
    public void Who_may_remove_whom(string? actorRole, string role, bool allowed)
        => Assert.Equal(allowed, MemberRules.CanRemove(actorRole, role));

    [Theory]
    [InlineData("owner", "admin", 1, true)]
    [InlineData("owner", null, 1, true)]
    [InlineData("owner", "owner", 1, false)]
    [InlineData("owner", "admin", 2, false)]
    [InlineData("owner", null, 2, false)]
    [InlineData("admin", null, 1, false)]
    public void Only_the_last_owner_is_kept(string currentRole, string? newRole, int owners, bool leavesNoOwner)
        => Assert.Equal(leavesNoOwner, MemberRules.LeavesNoOwner(currentRole, newRole, owners));

    [Fact]
    public void Every_role_can_be_given()
    {
        Assert.Equal(["owner", "admin", "member", "viewer"], MemberRules.Assignable);
        Assert.False(MemberRules.IsRole("boss"));
        Assert.False(MemberRules.IsRole(null));
    }

    [Theory]
    [InlineData("\"7\"", 7u, true)]
    [InlineData("W/\"7\"", 7u, true)]
    [InlineData("\"6\", \"7\"", 7u, true)]
    [InlineData("*", 7u, true)]
    [InlineData("\"6\"", 7u, false)]
    [InlineData("7", 7u, false)]
    public void If_match_accepts_the_members_etag(string ifMatch, uint version, bool accepted)
        => Assert.Equal(accepted, MemberVersion.IfMatchAccepts(ifMatch, version));
}
