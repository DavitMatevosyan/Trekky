using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Trekky.SharedKernel;

namespace Trekky.Persistence.Tenancy;

/// <summary>
/// Copies the authenticated user's <c>org_id</c> claim into the request's <see cref="TenantContext"/>.
/// Must run after authentication. Requests without the claim run with no tenant, which RLS treats as
/// "see nothing, write nothing".
/// </summary>
internal sealed class TenantContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, TenantContext tenant)
    {
        var claim = context.User.FindFirst(TrekkyClaimTypes.OrgId)?.Value;
        if (Guid.TryParse(claim, out var orgId) && orgId != Guid.Empty)
        {
            tenant.Set(orgId);
        }

        return next(context);
    }
}

public static class TenantContextApplicationBuilderExtensions
{
    /// <summary>Sets the tenant for each request from the <c>org_id</c> claim. Call after authentication.</summary>
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app)
        => app.UseMiddleware<TenantContextMiddleware>();
}
