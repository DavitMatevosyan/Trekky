using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Modules.Identity.Endpoints;
using Trekky.SharedKernel;

namespace Trekky.Modules.Identity;

/// <summary>
/// Users, organizations and memberships, plus sign-up, login and the in-app OpenIddict server (ADR-0003).
/// Owns the <c>identity</c> schema.
/// </summary>
public sealed class IdentityModule : IModule
{
    public const string Schema = "identity";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
        => services.AddIdentityModule(configuration);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapAuthEndpoints();

    public void MapRootEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapConnectEndpoints();
}
