using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Persistence.Tenancy;
using Trekky.SharedKernel;

namespace Trekky.Persistence.Registration;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>Runtime connection (role <c>trekky_app</c>, subject to RLS).</summary>
    public const string AppConnectionStringName = "Trekky";

    /// <summary>Migration connection (role <c>trekky_owner</c>, owns schemas and tables).</summary>
    public const string MigrationsConnectionStringName = "TrekkyMigrations";

    /// <summary>Registers the tenant context and the connection interceptor shared by all module DbContexts.</summary>
    public static IServiceCollection AddTrekkyPersistence(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<TenantConnectionInterceptor>();
        return services;
    }

    /// <summary>
    /// Registers a module's DbContext on the runtime connection with the tenant interceptor,
    /// snake_case naming and a migrations history table inside the module's schema.
    /// Call <see cref="AddTrekkyPersistence"/> once before this.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services, IConfiguration configuration, string schema)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        services.AddDbContext<TContext>((sp, options) =>
        {
            Configure(options, RequiredConnectionString(configuration, AppConnectionStringName), schema);
            options.AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>());
        });

        services.AddSingleton(new ModuleDbContextRegistration(
            typeof(TContext),
            schema,
            (sp, connectionString) =>
            {
                var options = new DbContextOptionsBuilder<TContext>();
                Configure(options, connectionString, schema);

                // Same application services as the runtime context, so contexts that read app options while
                // building their model (ASP.NET Core Identity reads IdentityOptions.Stores) get the same model.
                options.UseApplicationServiceProvider(sp);
                return ActivatorUtilities.CreateInstance<TContext>(sp, options.Options);
            }));

        return services;
    }

    internal static string RequiredConnectionString(IConfiguration configuration, string name)
        => configuration.GetConnectionString(name)
           ?? throw new InvalidOperationException($"Connection string '{name}' is not configured.");

    private static void Configure(DbContextOptionsBuilder options, string connectionString, string schema)
        => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseSnakeCaseNamingConvention();
}

/// <summary>A module DbContext the startup migrator knows how to build with the owner connection.</summary>
public sealed record ModuleDbContextRegistration(
    Type ContextType,
    string Schema,
    Func<IServiceProvider, string, DbContext> CreateForMigrations);
