using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Trekky.Modules.GitHub.Application.Contracts;
using Trekky.Modules.GitHub.Application.Repositories;
using Trekky.SharedKernel;

namespace Trekky.Modules.GitHub.Api.Endpoints;

/// <summary>A project's docs repository on GitHub, under /api/v1/projects/{projectId}/github (TREK-74).</summary>
internal static class ProjectRepositoryEndpoints
{
    public static IEndpointRouteBuilder MapProjectRepositoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var github = endpoints.MapGroup("/projects/{projectId:guid}/github")
            .WithTags("GitHub")
            .AddEndpointFilter(GitHubUnavailableFilter.InvokeAsync)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        github.MapGet("/", (Guid projectId, IProjectRepositoryService service, HttpResponse response,
                    CancellationToken cancellationToken)
                => GetAsync(projectId, service, response, cancellationToken))
            .WithName("GetProjectRepository")
            .RequireAuthorization(TrekkyPolicies.ReadWork)
            .WithSummary("Where the project's docs live on GitHub")
            .WithDescription("404 when the project has no repository linked (or is not the organization's). Sends the ETag for PUT.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        github.MapPut("/", (Guid projectId, [FromHeader(Name = "If-Match")] string? ifMatch, LinkProjectRepositoryRequest request,
                    IValidator<LinkProjectRepositoryRequest> validator, IProjectRepositoryService service,
                    ClaimsPrincipal principal, HttpResponse response, CancellationToken cancellationToken)
                => LinkAsync(projectId, ifMatch, request, validator, service, principal, response, cancellationToken))
            .WithName("LinkProjectRepository")
            .RequireAuthorization(TrekkyPolicies.ManageProjects)
            .WithSummary("Link the project to a repository, docs folder and branch")
            .WithDescription("Checked on GitHub: the connection must reach the repository and the branch and docs folder must "
                + "exist (422 with errors on the field otherwise). docsPath defaults to docs/ and branch to dev. 201 when "
                + "linked, 200 when changed; changing an existing link needs If-Match (428, 412 when stale).")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        github.MapDelete("/", (Guid projectId, IProjectRepositoryService service, ClaimsPrincipal principal,
                    CancellationToken cancellationToken)
                => UnlinkAsync(projectId, service, principal, cancellationToken))
            .WithName("UnlinkProjectRepository")
            .RequireAuthorization(TrekkyPolicies.ManageProjects)
            .WithSummary("Remove the project's repository link")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Ok<ProjectRepositoryLink>, ProblemHttpResult>> GetAsync(
        Guid projectId, IProjectRepositoryService service, HttpResponse response, CancellationToken cancellationToken)
    {
        var link = await service.GetAsync(projectId, cancellationToken);
        if (link is null)
        {
            return NotLinked();
        }

        response.Headers.ETag = link.ETag;
        return TypedResults.Ok(link.Link);
    }

    private static async Task<Results<Created<ProjectRepositoryLink>, Ok<ProjectRepositoryLink>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> LinkAsync(
        Guid projectId,
        string? ifMatch,
        LinkProjectRepositoryRequest request,
        IValidator<LinkProjectRepositoryRequest> validator,
        IProjectRepositoryService service,
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

        var result = await service.LinkAsync(projectId, request, ifMatch, actorId, cancellationToken);
        if (result.Link is { } link && result.Outcome is LinkRepositoryOutcome.Created or LinkRepositoryOutcome.Updated or LinkRepositoryOutcome.Stale)
        {
            // A stale save also sends the latest ETag, to retry with after showing the current settings.
            response.Headers.ETag = link.ETag;
        }

        return result.Outcome switch
        {
            LinkRepositoryOutcome.Created => TypedResults.Created($"/api/v1/projects/{projectId}/github", result.Link!.Link),
            LinkRepositoryOutcome.Updated => TypedResults.Ok(result.Link!.Link),
            LinkRepositoryOutcome.NotConfigured => GitHubProblems.NotConfigured(),
            LinkRepositoryOutcome.ProjectNotFound => TypedResults.Problem(title: "Project not found", statusCode: StatusCodes.Status404NotFound),
            LinkRepositoryOutcome.ConnectionNotFound => GitHubProblems.FieldRejected(
                "installationId", "This GitHub connection no longer exists. Choose another one."),
            LinkRepositoryOutcome.InstallationGone => GitHubProblems.InstallationGone(),
            LinkRepositoryOutcome.RepositoryNotFound => GitHubProblems.FieldRejected(
                "repository", "The Trekky app cannot reach this repository. Check the name, or give the app access to it on GitHub."),
            LinkRepositoryOutcome.BranchNotFound => GitHubProblems.FieldRejected(
                "branch", "This branch does not exist in the repository."),
            LinkRepositoryOutcome.DocsFolderNotFound => GitHubProblems.FieldRejected(
                "docsPath", "This folder does not exist on the branch. Create it on GitHub first, or choose another."),
            LinkRepositoryOutcome.IfMatchRequired => TypedResults.Problem(
                title: "If-Match required",
                detail: "The project already has a repository. Send the ETag from your last read in If-Match.",
                statusCode: StatusCodes.Status428PreconditionRequired),
            LinkRepositoryOutcome.Stale => TypedResults.Problem(
                title: "Settings changed",
                detail: "Someone changed the project's repository since you opened it. Review the current settings and save again.",
                statusCode: StatusCodes.Status412PreconditionFailed),
            _ => throw new InvalidOperationException($"Unhandled outcome {result.Outcome}."),
        };
    }

    private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult>> UnlinkAsync(
        Guid projectId, IProjectRepositoryService service, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(TrekkyClaimTypes.Subject), out var actorId))
        {
            return TypedResults.Unauthorized();
        }

        return await service.UnlinkAsync(projectId, actorId, cancellationToken) ? TypedResults.NoContent() : NotLinked();
    }

    private static ProblemHttpResult NotLinked()
        => TypedResults.Problem(title: "No GitHub repository linked", statusCode: StatusCodes.Status404NotFound);
}
