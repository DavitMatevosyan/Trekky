using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Trekky.Api.Tests;

/// <summary>
/// The real API (Development settings) against Postgres 17 in Docker, with the trekky_owner / trekky_app
/// roles from docker/postgres/init/01-roles.sql. Startup applies the module migrations, as in local dev.
/// One instance is shared by every test in <see cref="ApiCollection"/>; tests use fresh emails so they do not interfere.
/// </summary>
public sealed class TrekkyApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("trekky")
        .WithUsername("trekky")
        .WithPassword("trekky_dev")
        .Build();

    /// <summary>Connects as trekky_app, the runtime role RLS applies to.</summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    /// <summary>Connects as the Postgres superuser, which bypasses RLS: for asserting what is really stored.</summary>
    public string SuperuserConnectionString { get; private set; } = string.Empty;

    private string OwnerConnectionString { get; set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var superuser = _postgres.GetConnectionString();
        SuperuserConnectionString = superuser;

        await using (var connection = new NpgsqlConnection(superuser))
        {
            await connection.OpenAsync();
            var rolesSql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "01-roles.sql"));
            await using var command = new NpgsqlCommand(rolesSql, connection);
            await command.ExecuteNonQueryAsync();
        }

        AppConnectionString = WithLogin(superuser, "trekky_app", "trekky_app_dev");
        OwnerConnectionString = WithLogin(superuser, "trekky_owner", "trekky_owner_dev");
    }

    /// <summary>
    /// A client for https://localhost (the session cookie is Secure) that keeps cookies like a browser
    /// and does not follow redirects, so tests can read the authorization code from the redirect.
    /// </summary>
    public HttpClient CreateBrowserClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Trekky"] = AppConnectionString,
            ["ConnectionStrings:TrekkyMigrations"] = OwnerConnectionString,
        }));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private static string WithLogin(string connectionString, string username, string password)
        => new NpgsqlConnectionStringBuilder(connectionString) { Username = username, Password = password }.ConnectionString;
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<TrekkyApiFactory>
{
    public const string Name = "Api";
}
