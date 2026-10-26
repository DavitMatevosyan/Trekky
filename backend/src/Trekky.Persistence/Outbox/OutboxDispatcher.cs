using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Trekky.Persistence.Registration;
using Trekky.Persistence.Rls;
using Trekky.Persistence.Tenancy;
using Trekky.SharedKernel.Events;

namespace Trekky.Persistence.Outbox;

/// <summary>
/// Moves events from every module's outbox to the <see cref="IActivityEventSink"/>s (docs/tech/activity-events.md):
/// reads unprocessed rows in event order through the <c>outbox_dispatch</c> policy (ADR-0011), hands each one to the
/// sinks in a scope whose tenant is the event's organization, then marks it processed as that organization.
/// Delivery is at least once: a crash between the sink and the mark sends the event again.
/// </summary>
internal sealed partial class OutboxDispatcher(
    IServiceScopeFactory scopes,
    IEnumerable<OutboxSource> sources,
    IConfiguration configuration,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    public const int BatchSize = 100;

    private readonly string _connectionString = PersistenceServiceCollectionExtensions.RequiredConnectionString(
        configuration, PersistenceServiceCollectionExtensions.AppConnectionStringName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                while (await DispatchPendingAsync(stoppingToken) == BatchSize)
                {
                    // A full batch: there may be more waiting.
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogDispatchFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One pass over every outbox; returns the largest number of rows read from one of them.</summary>
    internal async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var largest = 0;
        foreach (var source in sources.DistinctBy(s => s.Schema))
        {
            var pending = await ReadPendingAsync(source.Schema, cancellationToken);
            largest = Math.Max(largest, pending.Count);

            foreach (var row in pending)
            {
                try
                {
                    await DeliverAsync(row, cancellationToken);
                    await MarkProcessedAsync(source.Schema, row, cancellationToken);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // One bad row must not hold back the events behind it: skip it and retry it next tick. History is
                    // ordered by event id, not by delivery, so a later event landing first is fine.
                    LogEventFailed(logger, row.Id, source.Schema, exception);
                }
            }
        }

        return largest;
    }

    private async Task DeliverAsync(PendingRow row, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(row.OrgId);
        var envelope = OutboxMessage.Deserialize(row.Envelope);
        foreach (var sink in scope.ServiceProvider.GetServices<IActivityEventSink>())
        {
            await sink.WriteAsync(envelope, cancellationToken);
        }
    }

    private async Task<List<PendingRow>> ReadPendingAsync(string schema, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var enable = new NpgsqlCommand(
            $"SELECT set_config('{OutboxRowLevelSecurity.SettingName}', 'on', true), set_config('{TenantRowLevelSecurity.SettingName}', '', true)",
            connection,
            transaction))
        {
            await enable.ExecuteNonQueryAsync(cancellationToken);
        }

        var rows = new List<PendingRow>();
        await using (var select = new NpgsqlCommand(
            $"SELECT id, org_id, envelope::text FROM {Table(schema)} WHERE processed_at IS NULL ORDER BY id LIMIT {BatchSize}",
            connection,
            transaction))
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new PendingRow(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2)));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return rows;
    }

    private async Task MarkProcessedAsync(string schema, PendingRow row, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var tenant = new NpgsqlCommand(
            $"SELECT set_config('{TenantRowLevelSecurity.SettingName}', @org_id, true)", connection, transaction))
        {
            tenant.Parameters.AddWithValue("org_id", row.OrgId.ToString());
            await tenant.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var update = new NpgsqlCommand(
            $"UPDATE {Table(schema)} SET processed_at = now() WHERE id = @id", connection, transaction))
        {
            update.Parameters.AddWithValue("id", row.Id);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static string Table(string schema) => TenantRowLevelSecurity.Qualified(schema, OutboxModelExtensions.TableName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Dispatching outbox events failed; retrying on the next tick")]
    private static partial void LogDispatchFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Delivering outbox event {EventId} from {Schema} failed; it stays pending")]
    private static partial void LogEventFailed(ILogger logger, Guid eventId, string schema, Exception exception);

    private sealed record PendingRow(Guid Id, Guid OrgId, string Envelope);
}
