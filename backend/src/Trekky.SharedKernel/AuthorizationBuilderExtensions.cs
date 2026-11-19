using Microsoft.AspNetCore.Authorization;

namespace Trekky.SharedKernel;

public static class AuthorizationBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="TrekkyPolicies"/>, each requiring an authenticated user whose <c>role</c> claim is one of its
    /// roles, and makes <see cref="TrekkyPolicies.ReadWork"/> the fallback for an endpoint that names no policy.
    /// </summary>
    public static AuthorizationBuilder AddTrekkyPolicies(this AuthorizationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (var (name, roles) in TrekkyPolicies.Roles)
        {
            builder.AddPolicy(name, policy => policy.RequireAuthenticatedUser().RequireClaim(TrekkyClaimTypes.Role, roles));
        }

        return builder.SetFallbackPolicy(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(TrekkyClaimTypes.Role, TrekkyPolicies.Roles[TrekkyPolicies.ReadWork])
            .Build());
    }
}
