using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Trekky.Modules.GitHub.Application.Contracts;
using Trekky.Modules.GitHub.Application.Docs;
using Trekky.SharedKernel;

namespace Trekky.Modules.GitHub.Api.Endpoints;

/// <summary>A project's docs from its GitHub repository, under /api/v1/projects/{projectId}/docs (TREK-75, ADR-0033).</summary>
internal static class DocsEndpoints
{
    /// <summary>Sent with every image: an SVG opened on its own runs no script and loads nothing.</summary>
    private const string ImageContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";

    public static IEndpointRouteBuilder MapDocsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var docs = endpoints.MapGroup("/projects/{projectId:guid}/docs")
            .WithTags("Docs")
            .AddEndpointFilter(GitHubUnavailableFilter.InvokeAsync)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // A read: it changes only the cache of what GitHub holds, never Trekky's own data, so readers may refresh too.
        docs.MapGet("/", async (Guid projectId, [FromQuery] bool? refresh, IDocsService service, CancellationToken cancellationToken)
                => ToResult(await service.GetTreeAsync(projectId, refresh ?? false, cancellationToken)))
            .WithName("GetDocsTree")
            .RequireAuthorization(TrekkyPolicies.ReadWork)
            .WithSummary("The project's docs folder: its Markdown files and last change")
            .WithDescription("Read from the cache; GitHub is asked again when the cache is older than 5 minutes, or now with "
                + "refresh=true. When GitHub cannot be reached, the cached copy is sent with stale: true. 404 when no "
                + "repository is linked.");

        docs.MapGet("/file", async (Guid projectId, [FromQuery] string? path, IDocsService service, HttpResponse response,
                    CancellationToken cancellationToken) =>
                {
                    var result = await service.GetDocAsync(projectId, path, cancellationToken);
                    if (result.Value is { } doc)
                    {
                        // The blob SHA: the version TREK-76's editor sends back to detect a conflicting change.
                        response.Headers.ETag = $"\"{doc.BlobSha}\"";
                    }

                    return ToResult(result);
                })
            .WithName("GetDoc")
            .RequireAuthorization(TrekkyPolicies.ReadWork)
            .WithSummary("One doc's Markdown and its last commit")
            .WithDescription("path is inside the docs folder, e.g. guide/setup.md. 400 for a path that is not a Markdown "
                + "file inside the folder. Sends the blob SHA as the ETag.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        docs.MapGet("/image", async (Guid projectId, [FromQuery] string? path, IDocsService service, HttpResponse response,
                    CancellationToken cancellationToken) =>
                {
                    var result = await service.GetImageAsync(projectId, path, cancellationToken);
                    if (result.Value is not { } image)
                    {
                        return (IResult)Problem(result.Outcome);
                    }

                    response.Headers.CacheControl = "private, max-age=300";
                    response.Headers.XContentTypeOptions = "nosniff";
                    response.Headers.ContentSecurityPolicy = ImageContentSecurityPolicy;
                    return TypedResults.File(image.Bytes, image.ContentType);
                })
            .WithName("GetDocImage")
            .RequireAuthorization(TrekkyPolicies.ReadWork)
            .WithSummary("An image from the docs folder (PNG, JPEG, GIF, WebP or SVG, up to 5 MB)")
            .WithDescription("Read at the commit the cached docs come from, so images match the docs shown with them. "
                + "400 for a path that is not an image inside the docs folder.")
            .Produces(StatusCodes.Status200OK, contentType: "image/png", additionalContentTypes: ["image/jpeg", "image/gif", "image/webp", "image/svg+xml"])
            .ProducesProblem(StatusCodes.Status400BadRequest);

        docs.MapGet("/search", async (Guid projectId, [FromQuery(Name = "q")] string? q, IValidator<SearchDocsQuery> validator,
                    IDocsService service, CancellationToken cancellationToken) =>
                {
                    var query = new SearchDocsQuery(q);
                    var validation = await validator.ValidateAsync(query, cancellationToken);
                    if (!validation.IsValid)
                    {
                        return (IResult)TypedResults.ValidationProblem(validation.ToDictionary());
                    }

                    return ToResult(await service.SearchAsync(projectId, query.Q!.Trim(), cancellationToken));
                })
            .WithName("SearchDocs")
            .RequireAuthorization(TrekkyPolicies.ReadWork)
            .WithSummary("Full-text search across the project's docs")
            .WithDescription("Every word must match, by prefix (\"dock comp\" finds \"Docker Compose\"). At most 20 docs, best "
                + "first, each with a snippet whose matched words are marked.")
            .Produces<DocSearchResults>()
            .ProducesValidationProblem();

        return endpoints;
    }

    private static Results<Ok<T>, ProblemHttpResult> ToResult<T>(DocsResult<T> result)
        where T : class
        => result.Value is { } value ? TypedResults.Ok(value) : Problem(result.Outcome);

    private static ProblemHttpResult Problem(DocsOutcome outcome) => outcome switch
    {
        DocsOutcome.NotConfigured => GitHubProblems.NotConfigured(),
        DocsOutcome.NotLinked => TypedResults.Problem(title: "No GitHub repository linked", statusCode: StatusCodes.Status404NotFound),
        DocsOutcome.InstallationGone => GitHubProblems.InstallationGone(),
        DocsOutcome.Unavailable => GitHubProblems.Unavailable(),
        DocsOutcome.InvalidPath => TypedResults.Problem(
            title: "Not a doc path",
            detail: "Give the path of a file inside the docs folder, like guide/setup.md.",
            statusCode: StatusCodes.Status400BadRequest),
        DocsOutcome.NotFound => TypedResults.Problem(title: "Doc not found", statusCode: StatusCodes.Status404NotFound),
        _ => throw new InvalidOperationException($"Unhandled outcome {outcome}."),
    };
}
