using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Modules.GitHub.Api.Endpoints;
using Trekky.Modules.GitHub.Application.Connections;
using Trekky.Modules.GitHub.Application.Contracts;
using Trekky.Modules.GitHub.Application.Repositories;
using Trekky.Modules.GitHub.Infrastructure;
using Trekky.SharedKernel;

namespace Trekky.Modules.GitHub.Api;

/// <summary>
/// The Trekky GitHub App connection (TREK-74, ADR-0027): installations per organization and each project's docs
/// repository. Owns the <c>github</c> schema.
/// </summary>
public sealed class GitHubModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<ConnectGitHubRequest>, ConnectGitHubRequestValidator>();
        services.AddSingleton<IValidator<LinkProjectRepositoryRequest>, LinkProjectRepositoryRequestValidator>();
        services.AddGitHubInfrastructure(configuration);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGitHubConnectionEndpoints();
        endpoints.MapProjectRepositoryEndpoints();
    }
}
