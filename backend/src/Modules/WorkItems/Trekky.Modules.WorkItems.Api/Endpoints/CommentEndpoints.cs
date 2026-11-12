using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Trekky.Modules.WorkItems.Application.Comments;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.SharedKernel;

namespace Trekky.Modules.WorkItems.Api.Endpoints;

/// <summary>Comments on work items under /api/v1: add, list, get, and the author's edit and delete (OpenAPI draft, TRK-5; US-12).</summary>
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

        endpoints.MapGet("/comments/{commentId:guid}", (Guid commentId, ICommentService service, HttpResponse response,
                    CancellationToken cancellationToken)
                => GetAsync(commentId, service, response, cancellationToken))
            .WithTags("Comments")
            .WithName("GetComment")
            .WithSummary("Get one comment (US-12)")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapPatch("/comments/{commentId:guid}", (Guid commentId, [FromHeader(Name = "If-Match")] string? ifMatch,
                    EditCommentRequest request, IValidator<EditCommentRequest> validator, ICommentService service,
                    ClaimsPrincipal principal, HttpResponse response, CancellationToken cancellationToken)
                => EditAsync(commentId, ifMatch, request, validator, service, principal, response, cancellationToken))
            .WithTags("Comments")
            .WithName("EditComment")
            .WithSummary("Edit your own comment (US-12)")
            .WithDescription(
                "JSON merge patch with the new bodyMd. Only the author may edit (403). Needs If-Match with the comment's etag (428, 412). Records CommentEdited on the item; an unchanged body changes nothing.")
            .Accepts<EditCommentRequest>(WorkItemEndpoints.MergePatchContentType)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        endpoints.MapDelete("/comments/{commentId:guid}", (Guid commentId, ICommentService service, ClaimsPrincipal principal,
                    CancellationToken cancellationToken)
                => DeleteAsync(commentId, service, principal, cancellationToken))
            .WithTags("Comments")
            .WithName("DeleteComment")
            .WithSummary("Delete your own comment (US-12)")
            .WithDescription("Only the author may delete (403). The comment is no longer shown; CommentDeleted is recorded on the item. Answers with the time it was deleted.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
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
        Guid commentId, ICommentService service, HttpResponse response, CancellationToken cancellationToken)
    {
        if (await service.GetAsync(commentId, cancellationToken) is not { } comment)
        {
            return CommentNotFound();
        }

        response.Headers.ETag = comment.Etag;
        return TypedResults.Ok(comment);
    }

    private static async Task<Results<Ok<Comment>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> EditAsync(
        Guid commentId,
        string? ifMatch,
        EditCommentRequest request,
        IValidator<EditCommentRequest> validator,
        ICommentService service,
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

        if (string.IsNullOrEmpty(ifMatch))
        {
            return TypedResults.Problem(
                title: "If-Match required",
                detail: "Send the comment's etag from your last read in If-Match.",
                statusCode: StatusCodes.Status428PreconditionRequired);
        }

        var result = await service.EditAsync(commentId, request.BodyMd!, ifMatch, actorId, cancellationToken);
        if (result.Comment is { } comment)
        {
            // On success the new version; when stale the latest one, to retry with.
            response.Headers.ETag = comment.Etag;
        }

        return result.Outcome == CommentOutcome.Succeeded && result.Comment is { } saved ? TypedResults.Ok(saved) : Failure(result.Outcome);
    }

    private static async Task<Results<Ok<DeletedComment>, ProblemHttpResult, UnauthorizedHttpResult>> DeleteAsync(
        Guid commentId, ICommentService service, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(TrekkyClaimTypes.Subject), out var actorId))
        {
            return TypedResults.Unauthorized();
        }

        var result = await service.DeleteAsync(commentId, actorId, cancellationToken);
        return result is { Outcome: CommentOutcome.Succeeded, Deleted: { } deleted } ? TypedResults.Ok(deleted) : Failure(result.Outcome);
    }

    private static ProblemHttpResult Failure(CommentOutcome outcome) => outcome switch
    {
        CommentOutcome.NotFound => CommentNotFound(),
        CommentOutcome.NotAuthor => TypedResults.Problem(
            title: "Not your comment",
            detail: "Only the author can change or delete a comment.",
            statusCode: StatusCodes.Status403Forbidden),
        CommentOutcome.Stale => TypedResults.Problem(
            title: "Changed elsewhere",
            detail: "This comment was changed since you read it. Its latest version is shown.",
            statusCode: StatusCodes.Status412PreconditionFailed),
        _ => throw new InvalidOperationException($"No problem response for {outcome}."),
    };

    private static ProblemHttpResult CommentNotFound()
        => TypedResults.Problem(title: "Comment not found", statusCode: StatusCodes.Status404NotFound);

    private static ProblemHttpResult WorkItemNotFound()
        => TypedResults.Problem(title: "Work item not found", statusCode: StatusCodes.Status404NotFound);
}
