using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Modules.WorkItems.Api.Endpoints;
using Trekky.Modules.WorkItems.Application.Comments;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.Modules.WorkItems.Application.WorkItems;
using Trekky.Modules.WorkItems.Infrastructure;
using Trekky.SharedKernel;

namespace Trekky.Modules.WorkItems.Api;

/// <summary>Work items of every type with parent/child rules and per-project keys, their board and comments (US-06 to US-12). Owns the <c>work_items</c> schema.</summary>
public sealed class WorkItemsModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<CreateWorkItemRequest>, CreateWorkItemRequestValidator>();
        services.AddSingleton<IValidator<WorkItemPatch>, WorkItemPatchValidator>();
        services.AddSingleton<IValidator<WorkItemFilter>, WorkItemFilterValidator>();
        services.AddSingleton<IValidator<ChangeStatusRequest>, ChangeStatusRequestValidator>();
        services.AddSingleton<IValidator<MoveWorkItemRequest>, MoveWorkItemRequestValidator>();
        services.AddSingleton<IValidator<AddCommentRequest>, AddCommentRequestValidator>();
        services.AddSingleton<IValidator<EditCommentRequest>, EditCommentRequestValidator>();
        services.AddWorkItemsInfrastructure(configuration);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapWorkItemEndpoints();
        endpoints.MapCommentEndpoints();
    }
}
