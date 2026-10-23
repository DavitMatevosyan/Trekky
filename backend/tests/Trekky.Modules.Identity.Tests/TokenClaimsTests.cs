using System.Security.Claims;
using OpenIddict.Abstractions;
using Trekky.Modules.Identity.Auth;
using Trekky.Modules.Identity.Domain;
using Trekky.Modules.Identity.Persistence;
using Trekky.SharedKernel;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Trekky.Modules.Identity.Tests;

public sealed class TokenClaimsTests
{
    private readonly User _user = new() { Email = "ada@example.com", DisplayName = "Ada" };
    private readonly MembershipInfo _membership = new(Guid.CreateVersion7(), Roles.Owner);

    [Fact]
    public void Access_token_carries_user_org_and_role()
    {
        var identity = TokenClaims.Apply(TokenClaims.Create(), _user, _membership);

        Assert.Equal(_user.Id.ToString(), identity.GetClaim(Claims.Subject));
        Assert.Equal(_membership.OrgId.ToString(), identity.GetClaim(TrekkyClaimTypes.OrgId));
        Assert.Equal(Roles.Owner, identity.GetClaim(Claims.Role));
        Assert.Equal("ada@example.com", identity.GetClaim(Claims.Email));

        foreach (var type in new[] { Claims.Subject, TrekkyClaimTypes.OrgId, Claims.Role, Claims.Email, Claims.Name })
        {
            Assert.Contains(Destinations.AccessToken, identity.FindFirst(type)!.GetDestinations());
        }
    }

    [Fact]
    public void Email_and_name_reach_the_identity_token_only_with_their_scopes()
    {
        var withoutScopes = TokenClaims.Apply(TokenClaims.Create(), _user, _membership);
        Assert.DoesNotContain(Destinations.IdentityToken, withoutScopes.FindFirst(Claims.Email)!.GetDestinations());
        Assert.DoesNotContain(Destinations.IdentityToken, withoutScopes.FindFirst(Claims.Name)!.GetDestinations());

        var withScopes = TokenClaims.Create();
        withScopes.SetScopes(Scopes.Email, Scopes.Profile);
        TokenClaims.Apply(withScopes, _user, _membership);
        Assert.Contains(Destinations.IdentityToken, withScopes.FindFirst(Claims.Email)!.GetDestinations());
        Assert.Contains(Destinations.IdentityToken, withScopes.FindFirst(Claims.Name)!.GetDestinations());
    }

    [Fact]
    public void Org_and_role_never_reach_the_identity_token()
    {
        var identity = TokenClaims.Create();
        identity.SetScopes(Scopes.Email, Scopes.Profile);
        TokenClaims.Apply(identity, _user, _membership);

        Assert.DoesNotContain(Destinations.IdentityToken, identity.FindFirst(TrekkyClaimTypes.OrgId)!.GetDestinations());
        Assert.DoesNotContain(Destinations.IdentityToken, identity.FindFirst(Claims.Role)!.GetDestinations());
    }

    [Fact]
    public void Refresh_replaces_stale_claims_with_current_ones()
    {
        var old = TokenClaims.Apply(TokenClaims.Create(), _user, new MembershipInfo(Guid.CreateVersion7(), Roles.Admin));

        var refreshed = TokenClaims.Apply(TokenClaims.CopyOf(new ClaimsPrincipal(old)), _user, _membership);

        Assert.Single(refreshed.FindAll(Claims.Role));
        Assert.Equal(Roles.Owner, refreshed.GetClaim(Claims.Role));
        Assert.Equal(_membership.OrgId.ToString(), refreshed.GetClaim(TrekkyClaimTypes.OrgId));
    }
}
