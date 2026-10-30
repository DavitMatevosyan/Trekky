using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Trekky.Modules.Projects.Application.Contracts;
using Trekky.Modules.Projects.Application.Projects;
using Trekky.SharedKernel;

namespace Trekky.Modules.Projects.Api.Endpoints;

/// <summary>Create, list and get projects, under /api/v1 (OpenAPI draft, TRK-5; US-05).</summary>
internal static class ProjectEndpoints
{
    internal const string KeyTakenMessage = "A project with this key already exists in your organization.";

    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var projects = endpoints.MapGroup("/projects")
            .WithTags("Projects")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        projects.MapPost("/", (CreateProjectRequest request, IValidator<CreateProjectRequest> validator, IProjectService service,
                    ClaimsPrincipal principal, HttpResponse response, CancellationToken cancellationToken)
                => CreateAsync(request, validator, service, principal, response, cancellationToken))
            .WithName("CreateProject")
            .WithSummary("Create a project (US-05)")
            .ProducesProblem(StatusCodes.Status409Conflict);

        projects.MapGet("/", (int? limit, string? cursor, IProjectService service, CancellationToken cancellationToken)
                => ListAsync(limit, cursor, service, cancellationToken))
            .WithName("ListProjects")
            .WithSummary("List projects by name (US-05)");

        projects.MapGet("/{projectId:guid}", (Guid projectId, IProjectService service, HttpResponse response,
                    CancellationToken cancellationToken)
                => GetAsync(projectId, service, response, cancellationToken))
            .WithName("GetProject")
            .WithSummary("Get a project")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Created<Project>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> CreateAsync(
        CreateProjectRequest request,
        IValidator<CreateProjectRequest> validator,
        IProjectService service,
        ClaimsPrincipal principal,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(TrekkyClaimTypes.Subject), out var actorId))
        {
            return TypedResults.Unauthorized();
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var (outcome, created) = await service.CreateAsync(request, actorId, cancellationToken);
        if (outcome == CreateProjectOutcome.KeyTaken || created is null)
        {
            return TypedResults.Problem(title: "Key already used", detail: KeyTakenMessage, statusCode: StatusCodes.Status409Conflict);
        }

        response.Headers.ETag = created.ETag;
        return TypedResults.Created($"/api/v1/projects/{created.Project.Id}", created.Project);
    }

    private static async Task<Results<Ok<ProjectPage>, ValidationProblem>> ListAsync(
        int? limit, string? cursor, IProjectService service, CancellationToken cancellationToken)
    {
        var pageSize = limit ?? ProjectCursor.DefaultLimit;
        if (pageSize is < 1 or > ProjectCursor.MaxLimit)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["limit"] = [$"Limit must be between 1 and {ProjectCursor.MaxLimit}."],
            });
        }

        if (!ProjectCursor.TryDecode(cursor, out var after))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["cursor"] = ["The cursor is not valid; use nextCursor from the previous page."],
            });
        }

        return TypedResults.Ok(await service.ListAsync(pageSize, after, cancellationToken));
    }

    private static async Task<Results<Ok<Project>, ProblemHttpResult>> GetAsync(
        Guid projectId, IProjectService service, HttpResponse response, CancellationToken cancellationToken)
    {
        // Another organization's project is invisible under RLS, so it is "not found" too.
        var project = await service.GetAsync(projectId, cancellationToken);
        if (project is null)
        {
            return TypedResults.Problem(title: "Project not found", statusCode: StatusCodes.Status404NotFound);
        }

        response.Headers.ETag = project.ETag;
        return TypedResults.Ok(project.Project);
    }
}
