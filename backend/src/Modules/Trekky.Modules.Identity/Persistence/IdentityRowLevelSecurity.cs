using Microsoft.EntityFrameworkCore.Migrations;
using Trekky.Persistence.Rls;

namespace Trekky.Modules.Identity.Persistence;

/// <summary>
/// Row-level security for the identity schema (ADR-0002).
/// <list type="bullet">
/// <item><c>organizations</c> and <c>memberships</c> get the standard <c>tenant_isolation</c> policy.</item>
/// <item><c>memberships</c> also gets <c>membership_self_read</c>: a user may <em>read</em> their own memberships
/// when <c>app.user_id</c> is set. Login and token refresh need it to find the user's organization before any
/// tenant is known. It grants SELECT only; writes still need the tenant.</item>
/// </list>
/// Users and OpenIddict tables are not tenant-scoped and have no RLS (data-model.md).
/// </summary>
internal static class IdentityRowLevelSecurity
{
    /// <summary>Postgres setting that carries the user whose memberships may be read.</summary>
    public const string UserIdSetting = "app.user_id";

    public const string MembershipSelfReadPolicy = "membership_self_read";

    public const string OrganizationsTable = "organizations";
    public const string MembershipsTable = "memberships";

    public const string EnableMembershipSelfReadSql = $"""
        CREATE POLICY {MembershipSelfReadPolicy} ON "{IdentityModule.Schema}"."{MembershipsTable}"
            FOR SELECT
            USING (user_id = NULLIF(current_setting('{UserIdSetting}', true), '')::uuid);
        """;

    public const string DisableMembershipSelfReadSql =
        $"""DROP POLICY IF EXISTS {MembershipSelfReadPolicy} ON "{IdentityModule.Schema}"."{MembershipsTable}";""";

    /// <summary>Call at the end of the migration's <c>Up</c> that creates the identity tables.</summary>
    public static MigrationBuilder EnableIdentityRowLevelSecurity(this MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.EnableTenantRowLevelSecurity(IdentityModule.Schema, OrganizationsTable, orgIdColumn: "id");
        migrationBuilder.EnableTenantRowLevelSecurity(IdentityModule.Schema, MembershipsTable);
        migrationBuilder.Sql(EnableMembershipSelfReadSql);
        return migrationBuilder;
    }

    /// <summary>Call at the start of the same migration's <c>Down</c>.</summary>
    public static MigrationBuilder DisableIdentityRowLevelSecurity(this MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(DisableMembershipSelfReadSql);
        migrationBuilder.DisableTenantRowLevelSecurity(IdentityModule.Schema, MembershipsTable);
        migrationBuilder.DisableTenantRowLevelSecurity(IdentityModule.Schema, OrganizationsTable);
        return migrationBuilder;
    }
}
