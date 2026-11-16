using Microsoft.EntityFrameworkCore.Migrations;
using Trekky.Persistence.Rls;

namespace Trekky.Persistence.Outbox;

/// <summary>
/// RLS for a module's outbox (ADR-0011): the usual tenant policy, plus a read-only <c>outbox_dispatch</c> policy that
/// shows <b>unprocessed</b> rows of every organization while <c>app.outbox_dispatch</c> is <c>on</c>. Only
/// <see cref="OutboxDispatcher"/> sets it, transaction-local. Marking a row processed still needs its <c>app.org_id</c>.
/// </summary>
public static class OutboxRowLevelSecurity
{
    public const string SettingName = "app.outbox_dispatch";

    public const string PolicyName = "outbox_dispatch";

    public static string EnableDispatchReadSql(string schema)
        => $"""
            CREATE POLICY {PolicyName} ON {TenantRowLevelSecurity.Qualified(schema, OutboxModelExtensions.TableName)}
                FOR SELECT
                USING (processed_at IS NULL AND current_setting('{SettingName}', true) = 'on');
            """;

    public static string DisableDispatchReadSql(string schema)
        => $"DROP POLICY IF EXISTS {PolicyName} ON {TenantRowLevelSecurity.Qualified(schema, OutboxModelExtensions.TableName)};";

    /// <summary>Tenant RLS plus the dispatcher's read policy on <c>{schema}.outbox</c>. Use in the migration that creates it.</summary>
    public static MigrationBuilder EnableOutboxRowLevelSecurity(this MigrationBuilder migrationBuilder, string schema)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.EnableTenantRowLevelSecurity(schema, OutboxModelExtensions.TableName);
        migrationBuilder.Sql(EnableDispatchReadSql(schema));
        return migrationBuilder;
    }

    public static MigrationBuilder DisableOutboxRowLevelSecurity(this MigrationBuilder migrationBuilder, string schema)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(DisableDispatchReadSql(schema));
        migrationBuilder.DisableTenantRowLevelSecurity(schema, OutboxModelExtensions.TableName);
        return migrationBuilder;
    }
}
