using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Trekky.Modules.GitHub.Application.Connections;
using Trekky.Modules.GitHub.Application.Contracts;
using Trekky.SharedKernel;

namespace Trekky.Modules.GitHub.Api.Endpoints;

/// <summary>The organization's GitHub App installations, under /api/v1/github (TREK-74). Admins and Owners only.</summary>
internal static class GitHubConnectionEndpoints
{
    public static IEndpointRouteBuilder MapGitHubConnectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var github = endpoints.MapGroup("/github")
            .WithTags("GitHub")
            .RequireAuthorization(TrekkyPolicies.ManageProjects)
            .AddEndpointFilter(GitHubUnavailableFilter.InvokeAsync)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        github.MapGet("/", (IGitHubConnectionService service, CancellationToken cancellationToken)
                => GetSettingsAsync(service, cancellationToken))
            .WithName("GetGitHubSettings")
            .WithSummary("The organization's GitHub connections and where to install the app");

        github.MapPost("/installations", (ConnectGitHubRequest request, IValidator<ConnectGitHubRequest> validator,
                    IGitHubConnectionService service, ClaimsPrincipal principal, CancellationToken cancellationToken)
                => ConnectAsync(request, validator, service, principal, cancellationToken))
            .WithName("ConnectGitHub")
            .WithSummary("Connect an installation of the Trekky GitHub App")
            .WithDescription("Send the code and installation_id from GitHub's redirect after the install. The installation is "
                + "connected only if the GitHub user behind the code administers every repository it reaches (422 otherwise). 201 when connected, "
                + "200 when the organization already had it, 409 when another organization has it, 503 when the server "
                + "has no GitHub App, 502 when GitHub fails.")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        github.MapDelete("/installations/{connectionId:guid}", (Guid connectionId, IGitHubConnectionService service,
                    ClaimsPrincipal principal, CancellationToken cancellationToken)
                => DisconnectAsync(connectionId, service, principal, cancellationToken))
            .WithName("DisconnectGitHub")
            .WithSummary("Disconnect an installation")
            .WithDescription("Unlinks the projects that used it and drops its cached token. The app stays installed on "
                + "GitHub until it is uninstalled there (manageUrl).")
            .ProducesProblem(StatusCodes.Status404NotFound);

        github.MapGet("/installations/{connectionId:guid}/repositories", (Guid connectionId, IGitHubConnectionService service,
                    CancellationToken cancellationToken)
                => ListRepositoriesAsync(connectionId, service, cancellationToken))
            .WithName("ListGitHubRepositories")
            .WithSummary("The repositories an installation can reach, by name")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<Ok<GitHubSettings>> GetSettingsAsync(IGitHubConnectionService service, CancellationToken cancellationToken)
        => TypedResults.Ok(await service.GetSettingsAsync(cancellationToken));

    private static async Task<Results<Created<GitHubConnection>, Ok<GitHubConnection>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> ConnectAsync(
        ConnectGitHubRequest request,
        IValidator<ConnectGitHubRequest> validator,
        IGitHubConnectionService service,
        ClaimsPrincipal principal,
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

        var (outcome, connection) = await service.ConnectAsync(request, actorId, cancellationToken);
        return outcome switch
        {
            ConnectGitHubOutcome.Connected => TypedResults.Created("/api/v1/github", connection!),
            ConnectGitHubOutcome.AlreadyConnected => TypedResults.Ok(connection!),
            ConnectGitHubOutcome.NotConfigured => GitHubProblems.NotConfigured(),
            ConnectGitHubOutcome.CodeRejected => TypedResults.Problem(
                title: "GitHub sign-in expired",
                detail: "GitHub did not accept the sign-in from the install. Connect GitHub again.",
                statusCode: StatusCodes.Status422UnprocessableEntity),
            ConnectGitHubOutcome.NotAccessible => TypedResults.Problem(
                title: "Installation not accessible",
                detail: "Your GitHub account must administer every repository this installation of the Trekky app can reach. Ask an owner of the GitHub account to connect it, or narrow the installation's repositories on GitHub.",
                statusCode: StatusCodes.Status422UnprocessableEntity),
            ConnectGitHubOutcome.ConnectedElsewhere => TypedResults.Problem(
                title: "Installation already connected",
                detail: "This GitHub installation is connected to another Trekky organization. Disconnect it there first.",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException($"Unhandled outcome {outcome}."),
        };
    }

    private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult>> DisconnectAsync(
        Guid connectionId, IGitHubConnectionService service, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(TrekkyClaimTypes.Subject), out var actorId))
        {
            return TypedResults.Unauthorized();
        }

        return await service.DisconnectAsync(connectionId, actorId, cancellationToken)
            ? TypedResults.NoContent()
            : GitHubProblems.ConnectionNotFound();
    }

    private static async Task<Results<Ok<GitHubRepositoryList>, ProblemHttpResult>> ListRepositoriesAsync(
        Guid connectionId, IGitHubConnectionService service, CancellationToken cancellationToken)
    {
        var (outcome, repositories) = await service.ListRepositoriesAsync(connectionId, cancellationToken);
        return outcome switch
        {
            ListRepositoriesOutcome.Listed => TypedResults.Ok(repositories!),
            ListRepositoriesOutcome.NotConfigured => GitHubProblems.NotConfigured(),
            ListRepositoriesOutcome.ConnectionNotFound => GitHubProblems.ConnectionNotFound(),
            ListRepositoriesOutcome.InstallationGone => GitHubProblems.InstallationGone(),
            _ => throw new InvalidOperationException($"Unhandled outcome {outcome}."),
        };
    }
}
