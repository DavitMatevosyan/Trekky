namespace Trekky.SharedKernel;

/// <summary>Claim types Trekky puts in access tokens (ADR-0003) besides the standard OIDC ones.</summary>
public static class TrekkyClaimTypes
{
    /// <summary>The organization (tenant) the token was issued for.</summary>
    public const string OrgId = "org_id";

    /// <summary>The standard OIDC subject: the user id. Here so modules can read it without referencing OpenIddict.</summary>
    public const string Subject = "sub";

    /// <summary>The caller's role in the organization (<see cref="TrekkyRoles"/>), checked by <see cref="TrekkyPolicies"/>.</summary>
    public const string Role = "role";
}
