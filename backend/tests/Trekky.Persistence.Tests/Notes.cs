using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Persistence.Registration;
using Trekky.Persistence.Tenancy;

namespace Trekky.Persistence.Tests;

/// <summary>A tenant-owned row, standing in for real module tables until TRK-10.</summary>
internal sealed class Note
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid OrgId { get; init; }
    public string Text { get; set; } = string.Empty;
}

internal sealed class NotesDbContext(DbContextOptions<NotesDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.HasDefaultSchema(PostgresFixture.Schema);
}

/// <summary>
/// Builds the DbContext exactly the way modules will — through AddTrekkyPersistence and
/// AddModuleDbContext — and opens one DI scope per simulated request.
/// </summary>
internal sealed class TenantScopes : IDisposable
{
    private readonly ServiceProvider _provider;

    public TenantScopes(string appConnectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{PersistenceServiceCollectionExtensions.AppConnectionStringName}"] = appConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTrekkyPersistence();
        services.AddModuleDbContext<NotesDbContext>(configuration, PostgresFixture.Schema);
        _provider = services.BuildServiceProvider();
    }

    /// <summary>A request scope for <paramref name="orgId"/>, or with no tenant when null.</summary>
    public RequestScope For(Guid? orgId)
    {
        var scope = _provider.CreateAsyncScope();
        if (orgId is { } id)
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(id);
        }

        return new RequestScope(scope);
    }

    public void Dispose() => _provider.Dispose();
}

internal sealed class RequestScope(AsyncServiceScope scope) : IAsyncDisposable
{
    public NotesDbContext Db => scope.ServiceProvider.GetRequiredService<NotesDbContext>();

    public ValueTask DisposeAsync() => scope.DisposeAsync();
}
