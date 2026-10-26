namespace Trekky.SharedKernel;

/// <summary>Claim types Trekky puts in access tokens (ADR-0003) besides the standard OIDC ones.</summary>
public static class TrekkyClaimTypes
{
    /// <summary>The organization (tenant) the token was issued for.</summary>
    public const string OrgId = "org_id";
}
