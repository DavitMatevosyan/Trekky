using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Trekky.Modules.Identity.Domain;
using Trekky.Modules.Identity.Persistence;
using Trekky.SharedKernel;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>The claims Trekky issues (ADR-0003): <c>sub</c>, <c>org_id</c>, <c>role</c>, <c>email</c>, <c>name</c>.</summary>
internal static class TokenClaims
{
    /// <summary>A fresh identity for the authorization endpoint.</summary>
    public static ClaimsIdentity Create()
        => new(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

    /// <summary>
    /// A copy of a redeemed code or refresh token's identity, keeping OpenIddict's own claims
    /// (scopes, authorization id) so the claims below can be refreshed from current data.
    /// </summary>
    public static ClaimsIdentity CopyOf(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return new ClaimsIdentity(principal.Claims, TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
    }

    /// <summary>Sets the user, organization and role claims from current data and decides where each claim goes.</summary>
    public static ClaimsIdentity Apply(ClaimsIdentity identity, User user, MembershipInfo membership)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(membership);

        identity
            .SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.Name, user.DisplayName)
            .SetClaim(TrekkyClaimTypes.OrgId, membership.OrgId.ToString())
            .SetClaim(Claims.Role, membership.Role);

        identity.SetDestinations(GetDestinations);
        return identity;
    }

    internal static IEnumerable<string> GetDestinations(Claim claim)
        => claim.Type switch
        {
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Name when claim.Subject?.HasScope(Scopes.Profile) == true => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Email when claim.Subject?.HasScope(Scopes.Email) == true => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Name or Claims.Email or Claims.Role or TrekkyClaimTypes.OrgId => [Destinations.AccessToken],
            _ => [],
        };
}
