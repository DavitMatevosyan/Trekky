using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Modules.Identity.Api.Endpoints;
using Trekky.Modules.Identity.Application.Accounts;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Infrastructure;
using Trekky.SharedKernel;

namespace Trekky.Modules.Identity.Api;

/// <summary>
/// Users, organizations and memberships, plus sign-up, login and the in-app OpenIddict server (ADR-0003).
/// Owns the <c>identity</c> schema.
/// </summary>
public sealed class IdentityModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<SignUpRequest>, SignUpRequestValidator>();
        services.AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddIdentityInfrastructure(configuration);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapAuthEndpoints();

    public void MapRootEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapConnectEndpoints();
}
