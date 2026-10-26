using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Persistence.Outbox;
using Trekky.Persistence.Registration;

namespace Trekky.Modules.Projects.Persistence;

internal static class ProjectsPersistenceServiceCollectionExtensions
{
    /// <summary>The projects schema's DbContext (RLS-aware, ADR-0002) and its outbox (drained into the activity log).</summary>
    public static IServiceCollection AddProjectsPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ProjectsDbContext>(configuration, ProjectsDbContext.Schema);
        services.AddModuleOutbox(ProjectsDbContext.Schema);
        return services;
    }
}
