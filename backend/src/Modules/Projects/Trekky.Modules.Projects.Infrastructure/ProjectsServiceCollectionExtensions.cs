using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trekky.Modules.Projects.Application.Projects;
using Trekky.Modules.Projects.Persistence;

namespace Trekky.Modules.Projects.Infrastructure;

internal static class ProjectsServiceCollectionExtensions
{
    public static IServiceCollection AddProjectsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddProjectsPersistence(configuration);
        services.AddScoped<IProjectService, ProjectService>();
        return services;
    }
}
