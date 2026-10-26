using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Modules.Activity.Infrastructure;
using Trekky.SharedKernel;

namespace Trekky.Modules.Activity.Api;

/// <summary>
/// The activity log: one append-only stream of every state change, fed by the modules' outboxes
/// (docs/tech/activity-events.md). Owns the <c>activity</c> schema. History endpoints come with TRK-12.
/// </summary>
public sealed class ActivityModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
        => services.AddActivityInfrastructure(configuration);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
