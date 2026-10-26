using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Trekky.Persistence.Registration;

public static class ModuleDatabaseMigrator
{
    /// <summary>
    /// Applies pending EF Core migrations for every registered module DbContext, using the
    /// <c>TrekkyMigrations</c> (owner) connection. Intended for Development startup only;
    /// other environments run migrations as a deploy step.
    /// </summary>
    public static async Task MigrateModuleDatabasesAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        var registrations = host.Services.GetServices<ModuleDbContextRegistration>().ToList();
        if (registrations.Count == 0)
        {
            return;
        }

        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var connectionString = PersistenceServiceCollectionExtensions.RequiredConnectionString(
            configuration, PersistenceServiceCollectionExtensions.MigrationsConnectionStringName);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ModuleDatabaseMigrator));
        await DatabaseReadiness.WaitAsync(connectionString, logger, cancellationToken);

        foreach (var registration in registrations)
        {
            await using var scope = host.Services.CreateAsyncScope();
            await using var context = registration.CreateForMigrations(scope.ServiceProvider, connectionString);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
