using Trekky.SharedKernel;

namespace Trekky.Persistence.Tenancy;

/// <summary>
/// Scoped holder of the current tenant. It can be set once per scope; setting a
/// different organization afterwards throws, so a request can never switch tenants halfway.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? OrgId { get; private set; }

    public void Set(Guid orgId)
    {
        if (orgId == Guid.Empty)
        {
            throw new ArgumentException("The organization id must not be empty.", nameof(orgId));
        }

        if (OrgId is { } current && current != orgId)
        {
            throw new InvalidOperationException("The tenant for this scope is already set to a different organization.");
        }

        OrgId = orgId;
    }
}
