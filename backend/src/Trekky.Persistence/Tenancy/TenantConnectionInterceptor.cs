using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Trekky.Persistence.Rls;
using Trekky.SharedKernel;

namespace Trekky.Persistence.Tenancy;

/// <summary>
/// Sets the Postgres session setting <c>app.org_id</c> every time EF Core opens a connection.
/// It always writes a value — the tenant's id, or an empty string when there is none — so a
/// pooled connection can never carry a previous request's tenant, even if the pool skipped its reset.
/// </summary>
internal sealed class TenantConnectionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    private const string SetTenantSql = "SELECT set_config('" + TenantRowLevelSecurity.SettingName + "', @org_id, false)";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = SetTenantSql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "org_id";
        parameter.Value = tenant.OrgId?.ToString() ?? string.Empty;
        command.Parameters.Add(parameter);

        return command;
    }
}
