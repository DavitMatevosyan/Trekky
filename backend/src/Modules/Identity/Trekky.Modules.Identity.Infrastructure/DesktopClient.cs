using System.Globalization;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>
/// The Windows desktop app's OAuth client (ADR-0017): public and native, authorization code + PKCE through the system
/// browser, back to the app on a <c>trekky://</c> deep link, with refresh tokens that last longer than the web app's so
/// the app starts signed in.
/// </summary>
internal static class DesktopClient
{
    public const string ClientId = "trekky-desktop";

    /// <summary>The deep link the app registers with Windows; used when configuration names none.</summary>
    public const string DefaultRedirectUri = "trekky://auth/callback";

    /// <summary>
    /// How long a refresh token lasts (ADR-0017). Each use rotates it and starts a new one, so this is how long the app may
    /// go unused before the user signs in again. The web app keeps OpenIddict's server-wide 14 days.
    /// </summary>
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(90);

    public static OpenIddictApplicationDescriptor CreateDescriptor(IEnumerable<string> redirectUris)
    {
        ArgumentNullException.ThrowIfNull(redirectUris);

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientType = ClientTypes.Public,
            ApplicationType = ApplicationTypes.Native,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "Trekky desktop app",
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange,
            },
            Settings =
            {
                [Settings.TokenLifetimes.RefreshToken] = RefreshTokenLifetime.ToString("c", CultureInfo.InvariantCulture),
            },
        };

        var uris = redirectUris.ToList();
        foreach (var uri in uris.Count > 0 ? uris : [DefaultRedirectUri])
        {
            descriptor.RedirectUris.Add(new Uri(uri, UriKind.Absolute));
        }

        return descriptor;
    }
}
