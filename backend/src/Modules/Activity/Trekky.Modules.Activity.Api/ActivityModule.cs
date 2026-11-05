using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Modules.Activity.Api.Endpoints;
using Trekky.Modules.Activity.Infrastructure;
using Trekky.SharedKernel;

namespace Trekky.Modules.Activity.Api;

/// <summary>
/// The activity log: one append-only stream of every state change, fed by the modules' outboxes
/// (docs/tech/activity-events.md), and item history read from it (US-09). Owns the <c>activity</c> schema.
/// </summary>
public sealed class ActivityModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
        => services.AddActivityInfrastructure(configuration);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapHistoryEndpoints();
}
