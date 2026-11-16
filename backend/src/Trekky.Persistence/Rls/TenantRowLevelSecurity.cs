using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Trekky.Persistence.Rls;

/// <summary>
/// SQL for the tenant isolation policy every tenant-owned table gets (ADR-0002).
/// Use <see cref="EnableTenantRowLevelSecurity"/> in the migration that creates the table.
/// </summary>
public static partial class TenantRowLevelSecurity
{
    /// <summary>The Postgres session setting that carries the current organization id.</summary>
    public const string SettingName = "app.org_id";

    public const string PolicyName = "tenant_isolation";

    // NULLIF: once set in a session, an unset custom setting reads as '' rather than NULL,
    // and ''::uuid would throw. NULL makes the policy deny instead (fail closed).
    private const string CurrentTenant = "NULLIF(current_setting('" + SettingName + "', true), '')::uuid";

    /// <summary>Enables and forces RLS on a table and adds the <c>tenant_isolation</c> policy.</summary>
    /// <param name="schema">Module schema, lowercase snake_case.</param>
    /// <param name="table">Table name, lowercase snake_case.</param>
    /// <param name="orgIdColumn">Column holding the organization id (<c>id</c> for the organizations table).</param>
    public static string EnableSql(string schema, string table, string orgIdColumn = "org_id")
    {
        var target = Qualified(schema, table);
        var column = Identifier(orgIdColumn, nameof(orgIdColumn));

        return $"""
            ALTER TABLE {target} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {target} FORCE ROW LEVEL SECURITY;
            CREATE POLICY {PolicyName} ON {target}
                USING ({column} = {CurrentTenant})
                WITH CHECK ({column} = {CurrentTenant});
            """;
    }

    /// <summary>Reverses <see cref="EnableSql"/> for a migration's <c>Down</c>.</summary>
    public static string DisableSql(string schema, string table)
    {
        var target = Qualified(schema, table);

        return $"""
            DROP POLICY IF EXISTS {PolicyName} ON {target};
            ALTER TABLE {target} NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE {target} DISABLE ROW LEVEL SECURITY;
            """;
    }

    public static MigrationBuilder EnableTenantRowLevelSecurity(
        this MigrationBuilder migrationBuilder, string schema, string table, string orgIdColumn = "org_id")
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(EnableSql(schema, table, orgIdColumn));
        return migrationBuilder;
    }

    public static MigrationBuilder DisableTenantRowLevelSecurity(
        this MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(DisableSql(schema, table));
        return migrationBuilder;
    }

    internal static string Qualified(string schema, string table)
        => $"{Identifier(schema, nameof(schema))}.{Identifier(table, nameof(table))}";

    // Names are interpolated into DDL, so only plain snake_case identifiers are accepted.
    internal static string Identifier(string name, string parameterName)
    {
        if (string.IsNullOrEmpty(name) || !SnakeCaseIdentifier().IsMatch(name))
        {
            throw new ArgumentException($"'{name}' is not a lowercase snake_case identifier.", parameterName);
        }

        return $"\"{name}\"";
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")]
    private static partial Regex SnakeCaseIdentifier();
}
