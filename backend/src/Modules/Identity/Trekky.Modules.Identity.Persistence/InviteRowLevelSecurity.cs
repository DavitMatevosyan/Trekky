using Microsoft.EntityFrameworkCore.Migrations;
using Trekky.Persistence.Rls;

namespace Trekky.Modules.Identity.Persistence;

/// <summary>
/// Row-level security for <c>identity.invites</c> (ADR-0027): the usual <c>tenant_isolation</c>, plus a read-only
/// <c>invite_token_read</c> policy that shows the one invite whose token hash is in <c>app.invite_token_hash</c>.
/// That is how the invite page and accepting an invite find the organization before any tenant is known, the same
/// way <c>user_self_read</c> serves login (ADR-0007). Writes still need <c>app.org_id</c>.
/// </summary>
internal static class InviteRowLevelSecurity
{
    public const string InvitesTable = "invites";

    /// <summary>The Postgres setting with the presented token's SHA-256 hash, lowercase hex. Set it transaction-local only.</summary>
    public const string TokenHashSettingName = "app.invite_token_hash";

    public const string TokenReadPolicyName = "invite_token_read";

    // An unset setting reads as '' or NULL; decode('', 'hex') is an empty bytea, which matches no 32-byte hash.
    internal static readonly string EnableTokenReadSql =
        $"CREATE POLICY {TokenReadPolicyName} ON \"{IdentityDbContext.Schema}\".\"{InvitesTable}\" FOR SELECT " +
        $"USING (token_hash = decode(COALESCE(current_setting('{TokenHashSettingName}', true), ''), 'hex'));";

    internal static readonly string DisableTokenReadSql =
        $"DROP POLICY IF EXISTS {TokenReadPolicyName} ON \"{IdentityDbContext.Schema}\".\"{InvitesTable}\";";

    /// <summary>Call at the end of the migration's <c>Up</c> that creates the invites table.</summary>
    public static MigrationBuilder EnableInviteRowLevelSecurity(this MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.EnableTenantRowLevelSecurity(IdentityDbContext.Schema, InvitesTable);
        migrationBuilder.Sql(EnableTokenReadSql);
        return migrationBuilder;
    }

    /// <summary>Call at the start of the same migration's <c>Down</c>.</summary>
    public static MigrationBuilder DisableInviteRowLevelSecurity(this MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(DisableTokenReadSql);
        migrationBuilder.DisableTenantRowLevelSecurity(IdentityDbContext.Schema, InvitesTable);
        return migrationBuilder;
    }
}
