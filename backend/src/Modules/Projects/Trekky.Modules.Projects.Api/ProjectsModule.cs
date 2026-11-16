using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Modules.Projects.Api.Endpoints;
using Trekky.Modules.Projects.Application.Contracts;
using Trekky.Modules.Projects.Application.Projects;
using Trekky.Modules.Projects.Infrastructure;
using Trekky.SharedKernel;

namespace Trekky.Modules.Projects.Api;

/// <summary>Projects with their immutable keys (US-05). Owns the <c>projects</c> schema.</summary>
public sealed class ProjectsModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<CreateProjectRequest>, CreateProjectRequestValidator>();
        services.AddProjectsInfrastructure(configuration);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapProjectEndpoints();
}
