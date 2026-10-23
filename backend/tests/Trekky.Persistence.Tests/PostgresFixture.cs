using Npgsql;
using Testcontainers.PostgreSql;
using Trekky.Persistence.Rls;

namespace Trekky.Persistence.Tests;

/// <summary>
/// Starts Postgres 17 in Docker once for the test collection, creates the trekky_owner and
/// trekky_app roles from docker/postgres/init/01-roles.sql, and — as the owner — a tenant
/// table protected by the same RLS policy real migrations use.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    internal const string Schema = "tenancy_test";
    internal const string Table = "notes";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("trekky")
        .WithUsername("trekky")
        .WithPassword("trekky_dev")
        .Build();

    /// <summary>Connects as trekky_app: the runtime role RLS applies to.</summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var superuser = _container.GetConnectionString();

        var rolesSql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "01-roles.sql"));
        await ExecuteAsync(superuser, rolesSql);

        var owner = WithLogin(superuser, "trekky_owner", "trekky_owner_dev");
        await ExecuteAsync(owner, $"""
            CREATE SCHEMA {Schema};
            CREATE TABLE {Schema}.{Table} (
                id     uuid PRIMARY KEY,
                org_id uuid NOT NULL,
                text   text NOT NULL
            );
            CREATE INDEX ix_{Table}_org_id ON {Schema}.{Table} (org_id);
            {TenantRowLevelSecurity.EnableSql(Schema, Table)}
            """);

        AppConnectionString = WithLogin(superuser, "trekky_app", "trekky_app_dev");
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private static string WithLogin(string connectionString, string username, string password)
        => new NpgsqlConnectionStringBuilder(connectionString) { Username = username, Password = password }.ConnectionString;

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
