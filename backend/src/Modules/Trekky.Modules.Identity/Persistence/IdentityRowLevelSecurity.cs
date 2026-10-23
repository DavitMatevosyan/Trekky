using Microsoft.EntityFrameworkCore.Migrations;
using Trekky.Persistence.Rls;

namespace Trekky.Modules.Identity.Persistence;

/// <summary>
/// Row-level security for the identity schema (ADR-0002, ADR-0007): <c>organizations</c> and
/// <c>memberships</c> get <c>tenant_isolation</c>; <c>memberships</c> also gets <c>user_self_read</c> so
/// login and token refresh can find the user's organization before a tenant is known.
/// Users and OpenIddict tables are not tenant-scoped and have no RLS (data-model.md).
/// </summary>
internal static class IdentityRowLevelSecurity
{
    public const string OrganizationsTable = "organizations";
    public const string MembershipsTable = "memberships";

    /// <summary>Call at the end of the migration's <c>Up</c> that creates the identity tables.</summary>
    public static MigrationBuilder EnableIdentityRowLevelSecurity(this MigrationBuilder migrationBuilder)
        => migrationBuilder
            .EnableTenantRowLevelSecurity(IdentityModule.Schema, OrganizationsTable, orgIdColumn: "id")
            .EnableTenantRowLevelSecurity(IdentityModule.Schema, MembershipsTable)
            .EnableUserSelfRead(IdentityModule.Schema, MembershipsTable);

    /// <summary>Call at the start of the same migration's <c>Down</c>.</summary>
    public static MigrationBuilder DisableIdentityRowLevelSecurity(this MigrationBuilder migrationBuilder)
        => migrationBuilder
            .DisableUserSelfRead(IdentityModule.Schema, MembershipsTable)
            .DisableTenantRowLevelSecurity(IdentityModule.Schema, MembershipsTable)
            .DisableTenantRowLevelSecurity(IdentityModule.Schema, OrganizationsTable);
}
