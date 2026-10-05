using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Trekky.SharedKernel;

/// <summary>
/// Contract every module of the modular monolith implements.
/// The host discovers modules and lets each one register its services and endpoints;
/// modules never reference each other's internals.
/// </summary>
public interface IModule
{
    /// <summary>Registers the module's services (DbContext, handlers, options).</summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps the module's HTTP endpoints under the shared /api/v1 group.</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
