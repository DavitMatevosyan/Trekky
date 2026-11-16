namespace Trekky.SharedKernel;

/// <summary>
/// The organization (tenant) the current unit of work runs for.
/// Set once per request from the access token's <c>org_id</c> claim; read by modules
/// when they create tenant rows. Postgres row-level security enforces it (ADR-0002).
/// </summary>
public interface ITenantContext
{
    /// <summary>The current organization, or <c>null</c> when no tenant is set.</summary>
    Guid? OrgId { get; }
}
