using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Modules.Activity.Persistence;
using Trekky.SharedKernel.Events;

namespace Trekky.Modules.Activity.Infrastructure;

internal static class ActivityServiceCollectionExtensions
{
    public static IServiceCollection AddActivityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddActivityPersistence(configuration);
        services.AddScoped<IActivityEventSink, ActivityEventSink>();
        return services;
    }
}
