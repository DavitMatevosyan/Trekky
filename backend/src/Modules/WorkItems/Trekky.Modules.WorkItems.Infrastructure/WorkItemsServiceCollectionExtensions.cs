using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trekky.Modules.Projects.Application.Contracts;
using Trekky.Modules.WorkItems.Application.Comments;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.Modules.WorkItems.Application.WorkItems;
using Trekky.Modules.WorkItems.Persistence;

namespace Trekky.Modules.WorkItems.Infrastructure;

internal static class WorkItemsServiceCollectionExtensions
{
    public static IServiceCollection AddWorkItemsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddWorkItemsPersistence(configuration);
        services.AddScoped<IWorkItemService, WorkItemService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IProjectItemCounts, WorkItemCounts>();
        services.AddScoped<IWorkItemDirectory, WorkItemDirectory>();
        return services;
    }
}
