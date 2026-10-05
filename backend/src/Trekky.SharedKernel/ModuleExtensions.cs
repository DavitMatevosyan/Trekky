using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Trekky.SharedKernel;

public static class ModuleExtensions
{
    public static IServiceCollection AddModules(
        this IServiceCollection services,
        IConfiguration configuration,
        params IModule[] modules)
    {
        foreach (var module in modules)
        {
            module.RegisterServices(services, configuration);
            services.AddSingleton(module);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder endpoints)
    {
        foreach (var module in endpoints.ServiceProvider.GetServices<IModule>())
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
