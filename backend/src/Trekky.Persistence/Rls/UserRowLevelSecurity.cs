using Microsoft.EntityFrameworkCore.Migrations;

namespace Trekky.Persistence.Rls;

/// <summary>
/// A read-only RLS policy that lets a user see their own rows in a tenant table before any tenant is
/// known (ADR-0007). Used only on <c>identity.memberships</c>: login and token refresh look up the user's
/// organization this way. It grants SELECT only; writes still need <c>app.org_id</c>. Add it next to,
/// never instead of, <see cref="TenantRowLevelSecurity.EnableSql"/>.
/// </summary>
public static class UserRowLevelSecurity
{
    /// <summary>The Postgres setting that carries the user whose rows may be read. Set it transaction-local only.</summary>
    public const string SettingName = "app.user_id";

    public const string PolicyName = "user_self_read";

    private const string CurrentUser = "NULLIF(current_setting('" + SettingName + "', true), '')::uuid";

    /// <summary>Adds the <c>user_self_read</c> SELECT policy. The table must already have tenant RLS enabled.</summary>
    public static string EnableSelfReadSql(string schema, string table, string userIdColumn = "user_id")
    {
        var target = TenantRowLevelSecurity.Qualified(schema, table);
        var column = TenantRowLevelSecurity.Identifier(userIdColumn, nameof(userIdColumn));

        return $"""
            CREATE POLICY {PolicyName} ON {target}
                FOR SELECT
                USING ({column} = {CurrentUser});
            """;
    }

    /// <summary>Reverses <see cref="EnableSelfReadSql"/> for a migration's <c>Down</c>.</summary>
    public static string DisableSelfReadSql(string schema, string table)
        => $"DROP POLICY IF EXISTS {PolicyName} ON {TenantRowLevelSecurity.Qualified(schema, table)};";

    public static MigrationBuilder EnableUserSelfRead(
        this MigrationBuilder migrationBuilder, string schema, string table, string userIdColumn = "user_id")
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(EnableSelfReadSql(schema, table, userIdColumn));
        return migrationBuilder;
    }

    public static MigrationBuilder DisableUserSelfRead(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(DisableSelfReadSql(schema, table));
        return migrationBuilder;
    }
}
