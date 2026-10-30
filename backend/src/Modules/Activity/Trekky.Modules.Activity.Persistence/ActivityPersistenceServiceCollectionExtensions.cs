using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Persistence.Registration;

namespace Trekky.Modules.Activity.Persistence;

internal static class ActivityPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddActivityPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ActivityDbContext>(configuration, ActivityDbContext.Schema);
        services.AddScoped<ActivityEventWriter>();
        return services;
    }
}
