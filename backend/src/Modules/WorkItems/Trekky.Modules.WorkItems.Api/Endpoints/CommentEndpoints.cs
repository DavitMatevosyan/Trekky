using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Trekky.Modules.WorkItems.Application.Comments;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.SharedKernel;

namespace Trekky.Modules.WorkItems.Api.Endpoints;

/// <summary>Comments on work items under /api/v1 (OpenAPI draft, TRK-5; US-12).</summary>
internal static class CommentEndpoints
{
    public static IEndpointRouteBuilder MapCommentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/work-items/{workItemId:guid}/comments", (Guid workItemId, int? limit, string? cursor,
                    ICommentService service, CancellationToken cancellationToken)
                => ListAsync(workItemId, limit, cursor, service, cancellationToken))
            .WithTags("Comments")
            .WithName("ListComments")
            .WithSummary("An item's comments, oldest first (US-12)")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapPost("/work-items/{workItemId:guid}/comments", (Guid workItemId, AddCommentRequest request,
                    IValidator<AddCommentRequest> validator, ICommentService service, ClaimsPrincipal principal,
                    CancellationToken cancellationToken)
                => AddAsync(workItemId, request, validator, service, principal, cancellationToken))
            .WithTags("Comments")
            .WithName("AddComment")
            .WithSummary("Comment on an item in Markdown (US-12)")
            .WithDescription("Records a CommentAdded event on the item, so the comment shows in its history.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapGet("/comments/{commentId:guid}", (Guid commentId, ICommentService service, CancellationToken cancellationToken)
                => GetAsync(commentId, service, cancellationToken))
            .WithTags("Comments")
            .WithName("GetComment")
            .WithSummary("Get one comment (US-12)")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Ok<CommentPage>, ValidationProblem, ProblemHttpResult>> ListAsync(
        Guid workItemId, int? limit, string? cursor, ICommentService service, CancellationToken cancellationToken)
    {
        // Every query-string problem in one answer.
        var errors = new Dictionary<string, string[]>();
        var pageSize = limit ?? CommentCursor.DefaultLimit;
        if (pageSize is < 1 or > CommentCursor.MaxLimit)
        {
            errors["limit"] = [$"Limit must be between 1 and {CommentCursor.MaxLimit}."];
        }

        if (!CommentCursor.TryDecode(cursor, out var after))
        {
            errors["cursor"] = ["The cursor is not valid; use nextCursor from the previous page."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var page = await service.ListAsync(workItemId, pageSize, after, cancellationToken);
        return page is null ? WorkItemNotFound() : TypedResults.Ok(page);
    }

    private static async Task<Results<Created<Comment>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> AddAsync(
        Guid workItemId,
        AddCommentRequest request,
        IValidator<AddCommentRequest> validator,
        ICommentService service,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(TrekkyClaimTypes.Subject), out var authorId))
        {
            return TypedResults.Unauthorized();
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var comment = await service.AddAsync(workItemId, request, authorId, cancellationToken);
        return comment is null ? WorkItemNotFound() : TypedResults.Created($"/api/v1/comments/{comment.Id}", comment);
    }

    private static async Task<Results<Ok<Comment>, ProblemHttpResult>> GetAsync(
        Guid commentId, ICommentService service, CancellationToken cancellationToken)
        => await service.GetAsync(commentId, cancellationToken) is { } comment
            ? TypedResults.Ok(comment)
            : TypedResults.Problem(title: "Comment not found", statusCode: StatusCodes.Status404NotFound);

    private static ProblemHttpResult WorkItemNotFound()
        => TypedResults.Problem(title: "Work item not found", statusCode: StatusCodes.Status404NotFound);
}
