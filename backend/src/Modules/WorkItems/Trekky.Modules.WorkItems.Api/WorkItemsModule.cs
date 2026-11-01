using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Modules.WorkItems.Api.Endpoints;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.Modules.WorkItems.Application.WorkItems;
using Trekky.Modules.WorkItems.Infrastructure;
using Trekky.SharedKernel;

namespace Trekky.Modules.WorkItems.Api;

/// <summary>Work items of every type with parent/child rules and per-project keys (US-06 to US-09). Owns the <c>work_items</c> schema.</summary>
public sealed class WorkItemsModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<CreateWorkItemRequest>, CreateWorkItemRequestValidator>();
        services.AddSingleton<IValidator<WorkItemPatch>, WorkItemPatchValidator>();
        services.AddWorkItemsInfrastructure(configuration);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapWorkItemEndpoints();
}
