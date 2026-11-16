using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Persistence.Outbox;
using Trekky.Persistence.Registration;

namespace Trekky.Modules.WorkItems.Persistence;

internal static class WorkItemsPersistenceServiceCollectionExtensions
{
    /// <summary>The work_items schema's DbContext (RLS-aware, ADR-0002) and its outbox (drained into the activity log).</summary>
    public static IServiceCollection AddWorkItemsPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<WorkItemsDbContext>(configuration, WorkItemsDbContext.Schema);
        services.AddModuleOutbox(WorkItemsDbContext.Schema);
        return services;
    }
}
