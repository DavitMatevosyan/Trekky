using Microsoft.Extensions.Logging;
using Npgsql;

namespace Trekky.Persistence.Registration;

/// <summary>
/// Waits for Postgres before the dev migrator runs: <c>docker compose up</c> (and Rider's Infra run configuration)
/// returns before the server accepts connections, and on first start the container restarts once after running
/// the init scripts. Only transient errors are retried; a wrong password or missing role fails at once. Each probe
/// times out after a few seconds and every retry logs a warning, so a wrong host or port fails visibly within about
/// a minute and a half.
/// </summary>
internal static partial class DatabaseReadiness
{
    public const int MaxAttempts = 30;

    public const int ProbeTimeoutSeconds = 2;

    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    public static Task WaitAsync(string connectionString, ILogger logger, CancellationToken cancellationToken)
    {
        var probe = new NpgsqlConnectionStringBuilder(connectionString) { Timeout = ProbeTimeoutSeconds };
        return WaitAsync(
            async token =>
            {
                await using var connection = new NpgsqlConnection(probe.ConnectionString);
                await connection.OpenAsync(token);
            },
            MaxAttempts,
            RetryDelay,
            (attempt, exception) => LogRetry(logger, probe.Host, probe.Port, attempt, MaxAttempts, exception.Message),
            cancellationToken);
    }

    public static async Task WaitAsync(
        Func<CancellationToken, Task> connect,
        int maxAttempts,
        TimeSpan retryDelay,
        Action<int, NpgsqlException> onRetry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connect);
        ArgumentNullException.ThrowIfNull(onRetry);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await connect(cancellationToken);
                return;
            }
            catch (NpgsqlException exception) when (exception.IsTransient && attempt < maxAttempts)
            {
                onRetry(attempt, exception);
                await Task.Delay(retryDelay, cancellationToken);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Postgres at {Host}:{Port} is not accepting connections yet (attempt {Attempt} of {MaxAttempts}): {Reason}")]
    private static partial void LogRetry(ILogger logger, string? host, int port, int attempt, int maxAttempts, string reason);
}
