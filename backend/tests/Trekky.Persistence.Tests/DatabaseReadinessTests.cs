using System.Net.Sockets;
using Npgsql;
using Trekky.Persistence.Registration;

namespace Trekky.Persistence.Tests;

public sealed class DatabaseReadinessTests
{
    private static readonly NpgsqlException ConnectionRefused = new("Failed to connect", new SocketException((int)SocketError.ConnectionRefused));

    [Fact]
    public async Task Retries_transient_failures_until_the_database_accepts_connections()
    {
        var attempts = 0;
        var retries = new List<int>();

        await DatabaseReadiness.WaitAsync(
            _ => ++attempts < 3 ? throw ConnectionRefused : Task.CompletedTask,
            maxAttempts: 5,
            TimeSpan.Zero,
            (attempt, _) => retries.Add(attempt),
            CancellationToken.None);

        Assert.Equal(3, attempts);
        Assert.Equal([1, 2], retries);
    }

    [Fact]
    public async Task Fails_at_once_on_a_non_transient_error()
    {
        var attempts = 0;
        var retries = 0;

        await Assert.ThrowsAsync<NpgsqlException>(() => DatabaseReadiness.WaitAsync(
            _ =>
            {
                attempts++;
                throw new NpgsqlException("password authentication failed");
            },
            maxAttempts: 5,
            TimeSpan.Zero,
            (_, _) => retries++,
            CancellationToken.None));

        Assert.Equal(1, attempts);
        Assert.Equal(0, retries);
    }

    [Fact]
    public async Task Gives_up_after_the_last_attempt()
    {
        var attempts = 0;
        var retries = 0;

        await Assert.ThrowsAsync<NpgsqlException>(() => DatabaseReadiness.WaitAsync(
            _ =>
            {
                attempts++;
                throw ConnectionRefused;
            },
            maxAttempts: 3,
            TimeSpan.Zero,
            (_, _) => retries++,
            CancellationToken.None));

        Assert.Equal(3, attempts);
        Assert.Equal(2, retries);
    }
}
