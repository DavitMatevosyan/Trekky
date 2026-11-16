namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>Named authorization policies of the Identity module.</summary>
internal static class AuthorizationPolicies
{
    /// <summary>
    /// The bearer access token or the session cookie. Only for logout: a tab whose tokens are already gone
    /// (expired or revoked refresh token) must still be able to end the session cookie (TRK-8.1). The cookie is
    /// SameSite=Strict, so other sites cannot send it.
    /// </summary>
    public const string BearerOrSessionCookie = "BearerOrSessionCookie";
}
