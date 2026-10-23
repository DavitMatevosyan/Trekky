using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Trekky.Modules.Identity.Auth;
using Trekky.Modules.Identity.Domain;
using Trekky.Modules.Identity.Persistence;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Trekky.Modules.Identity.Endpoints;

/// <summary>
/// Passthrough handlers for OpenIddict's authorization and token endpoints (ADR-0003). OpenIddict validates the
/// protocol (client, redirect URI, PKCE, codes, refresh-token rotation); these decide who the user is and which
/// claims they get. Revocation, discovery and JWKS are served by OpenIddict itself.
/// </summary>
internal static class ConnectEndpoints
{
    public static IEndpointRouteBuilder MapConnectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], AuthorizeAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();

        endpoints.MapPost("/connect/token", ExchangeAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }

    /// <summary>Issues an authorization code for the user logged in with the session cookie.</summary>
    private static async Task<Results<SignInHttpResult, ForbidHttpResult>> AuthorizeAsync(
        HttpContext http,
        SignInManager<User> signIn,
        MembershipReader memberships,
        IOpenIddictApplicationManager applications,
        IOpenIddictAuthorizationManager authorizations,
        CancellationToken cancellationToken)
    {
        var request = http.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // The web app logs in through POST /api/v1/auth/login first; there is no server-rendered login page.
        // Without a valid session the client gets login_required (also the answer to prompt=none).
        // The security stamp is checked on every call, so a cookie from before logout stops working at once.
        var session = await http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var user = session.Succeeded ? await signIn.ValidateSecurityStampAsync(session.Principal) : null;
        if (user is null || !await signIn.CanSignInAsync(user))
        {
            return Forbid(Errors.LoginRequired, "The user is not logged in.");
        }

        var membership = await memberships.FindForUserAsync(user.Id, cancellationToken);
        if (membership is null)
        {
            return Forbid(Errors.AccessDenied, "The user does not belong to an organization.");
        }

        var application = await applications.FindByClientIdAsync(request.ClientId!, cancellationToken)
            ?? throw new InvalidOperationException("The client application cannot be found.");

        var identity = TokenClaims.Create();
        identity.SetScopes(request.GetScopes());
        TokenClaims.Apply(identity, user, membership);

        // One ad-hoc authorization per login: refresh tokens chain to it, so reuse detection and logout can revoke it.
        var authorization = await authorizations.CreateAsync(
            identity,
            user.Id.ToString(),
            (await applications.GetIdAsync(application, cancellationToken))!,
            AuthorizationTypes.AdHoc,
            identity.GetScopes(),
            cancellationToken);
        identity.SetAuthorizationId(await authorizations.GetIdAsync(authorization, cancellationToken));

        return TypedResults.SignIn(new ClaimsPrincipal(identity), properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>Exchanges a code or a refresh token, re-reading the user and membership so tokens never outlive them.</summary>
    private static async Task<Results<SignInHttpResult, ForbidHttpResult>> ExchangeAsync(
        HttpContext http,
        UserManager<User> users,
        SignInManager<User> signIn,
        MembershipReader memberships,
        CancellationToken cancellationToken)
    {
        var request = http.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            throw new InvalidOperationException("The grant type is not supported.");
        }

        var result = await http.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var subject = result.Principal?.GetClaim(Claims.Subject);
        var user = subject is null ? null : await users.FindByIdAsync(subject);
        if (result.Principal is null || user is null || !await signIn.CanSignInAsync(user))
        {
            return Forbid(Errors.InvalidGrant, "The token is no longer valid.");
        }

        var membership = await memberships.FindForUserAsync(user.Id, cancellationToken);
        if (membership is null)
        {
            return Forbid(Errors.InvalidGrant, "The user no longer belongs to an organization.");
        }

        var identity = TokenClaims.Apply(TokenClaims.CopyOf(result.Principal), user, membership);
        return TypedResults.SignIn(new ClaimsPrincipal(identity), properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static ForbidHttpResult Forbid(string error, string description)
        => TypedResults.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
