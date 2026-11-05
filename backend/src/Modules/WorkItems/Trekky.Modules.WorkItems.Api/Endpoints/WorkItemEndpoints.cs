using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.Modules.WorkItems.Application.WorkItems;
using Trekky.SharedKernel;

namespace Trekky.Modules.WorkItems.Api.Endpoints;

/// <summary>Create, list, get, edit and move work items, under /api/v1 (OpenAPI draft, TRK-5; US-06 to US-10).</summary>
internal static class WorkItemEndpoints
{
    internal const string MergePatchContentType = "application/merge-patch+json";

    internal const string StaleMessage = "This item was changed by someone else.";

    private static readonly JsonDocumentOptions NoDuplicateFields = new() { AllowDuplicateProperties = false };

    public static IEndpointRouteBuilder MapWorkItemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var inProject = endpoints.MapGroup("/projects/{projectId:guid}/work-items")
            .WithTags("WorkItems")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        inProject.MapPost("/", (Guid projectId, CreateWorkItemRequest request, IValidator<CreateWorkItemRequest> validator,
                    IWorkItemService service, ClaimsPrincipal principal, HttpResponse response, CancellationToken cancellationToken)
                => CreateAsync(projectId, request, validator, service, principal, response, cancellationToken))
            .WithName("CreateWorkItem")
            .WithSummary("Create a work item in To Do with the project's next key (US-06)")
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        inProject.MapGet("/", (Guid projectId, int? limit, string? cursor, [FromQuery(Name = "type")] string[]? types,
                    [FromQuery(Name = "q")] string? search, IValidator<WorkItemFilter> validator, IWorkItemService service,
                    CancellationToken cancellationToken)
                => ListAsync(projectId, limit, cursor, WorkItemFilter.From(types, search), validator, service, cancellationToken))
            .WithName("ListWorkItems")
            .WithSummary("List a project's work items, newest update first (US-08)")
            .WithDescription(
                "Optional filters (TRK-11.1, e.g. parent candidates): repeat `type` to keep only those types; `q` keeps items whose key starts with it or whose title contains it, case-insensitive; an exact key match comes first.");

        var items = endpoints.MapGroup("/work-items")
            .WithTags("WorkItems")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        items.MapGet("/{workItemId:guid}", (Guid workItemId, IWorkItemService service, HttpResponse response,
                    CancellationToken cancellationToken)
                => GetAsync(workItemId, service, response, cancellationToken))
            .WithName("GetWorkItem")
            .WithSummary("Get a work item with its parent and children (US-09)");

        items.MapGet("/by-key/{key}", (string key, IWorkItemService service, HttpResponse response, CancellationToken cancellationToken)
                => GetByKeyAsync(key, service, response, cancellationToken))
            .WithName("GetWorkItemByKey")
            .WithSummary("Get a work item by its key, e.g. TRK-42 (US-09 AC3)");

        items.MapPatch("/{workItemId:guid}", (Guid workItemId, [FromHeader(Name = "If-Match")] string? ifMatch, HttpRequest request,
                    IValidator<WorkItemPatch> validator, IWorkItemService service, ClaimsPrincipal principal, HttpResponse response,
                    CancellationToken cancellationToken)
                => UpdateAsync(workItemId, ifMatch, request, validator, service, principal, response, cancellationToken))
            .WithName("UpdateWorkItem")
            .WithSummary("Edit title, description, type or parent (US-07)")
            .WithDescription("JSON merge patch: only the fields sent change; null clears descriptionMd or parentId. Needs If-Match.")
            .Accepts<UpdateWorkItemRequest>(MergePatchContentType)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        items.MapPost("/{workItemId:guid}/status", (Guid workItemId, [FromHeader(Name = "If-Match")] string? ifMatch,
                    ChangeStatusRequest request, IValidator<ChangeStatusRequest> validator, IWorkItemService service,
                    ClaimsPrincipal principal, HttpResponse response, CancellationToken cancellationToken)
                => ChangeStatusAsync(workItemId, ifMatch, request, validator, service, principal, response, cancellationToken))
            .WithName("ChangeWorkItemStatus")
            .WithSummary("Move an item through the workflow (US-10)")
            .WithDescription("Only allowed moves succeed; otherwise 422 lists allowedTransitions. Needs If-Match.")
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        return endpoints;
    }

    private static async Task<Results<Created<WorkItemDetail>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> CreateAsync(
        Guid projectId,
        CreateWorkItemRequest request,
        IValidator<CreateWorkItemRequest> validator,
        IWorkItemService service,
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

        var result = await service.CreateAsync(projectId, request, actorId, cancellationToken);
        if (result is not { Outcome: WorkItemOutcome.Succeeded, Item: { } created })
        {
            return Failure(result);
        }

        response.Headers.ETag = created.ETag;
        return TypedResults.Created($"/api/v1/work-items/{created.Item.Id}", created.Item);
    }

    private static async Task<Results<Ok<WorkItemPage>, ValidationProblem, ProblemHttpResult>> ListAsync(
        Guid projectId,
        int? limit,
        string? cursor,
        WorkItemFilter filter,
        IValidator<WorkItemFilter> validator,
        IWorkItemService service,
        CancellationToken cancellationToken)
    {
        // Every query-string problem in one answer.
        var errors = (await validator.ValidateAsync(filter, cancellationToken)).ToDictionary();
        var pageSize = limit ?? WorkItemCursor.DefaultLimit;
        if (pageSize is < 1 or > WorkItemCursor.MaxLimit)
        {
            errors["limit"] = [$"Limit must be between 1 and {WorkItemCursor.MaxLimit}."];
        }

        if (!WorkItemCursor.TryDecode(cursor, out var after))
        {
            errors["cursor"] = ["The cursor is not valid; use nextCursor from the previous page."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var page = await service.ListAsync(projectId, filter, pageSize, after, cancellationToken);
        return page is null ? ProjectNotFound() : TypedResults.Ok(page);
    }

    private static async Task<Results<Ok<WorkItemDetail>, ProblemHttpResult>> GetAsync(
        Guid workItemId, IWorkItemService service, HttpResponse response, CancellationToken cancellationToken)
        => Found(await service.GetAsync(workItemId, cancellationToken), response);

    private static async Task<Results<Ok<WorkItemDetail>, ProblemHttpResult>> GetByKeyAsync(
        string key, IWorkItemService service, HttpResponse response, CancellationToken cancellationToken)
        => Found(await service.GetByKeyAsync(key, cancellationToken), response);

    private static async Task<Results<Ok<WorkItemDetail>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> UpdateAsync(
        Guid workItemId,
        string? ifMatch,
        HttpRequest request,
        IValidator<WorkItemPatch> validator,
        IWorkItemService service,
        ClaimsPrincipal principal,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(TrekkyClaimTypes.Subject), out var actorId))
        {
            return TypedResults.Unauthorized();
        }

        // Read the body ourselves: a bound record could not tell "field not sent" from "field set to null".
        if (!request.HasJsonContentType())
        {
            return TypedResults.Problem(
                title: "Unsupported media type",
                detail: $"Send the edit as {MergePatchContentType}.",
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        JsonNode? body;
        try
        {
            // A repeated field ({"title":"a","title":"b"}) is malformed: which one would win is a guess.
            body = await JsonNode.ParseAsync(request.Body, documentOptions: NoDuplicateFields, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [WorkItemPatchReader.BodyField] = ["The body is not valid JSON."],
            });
        }

        if (!WorkItemPatchReader.TryRead(body, out var patch, out var errors))
        {
            return TypedResults.ValidationProblem(errors);
        }

        var validation = await validator.ValidateAsync(patch, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        if (string.IsNullOrEmpty(ifMatch))
        {
            return IfMatchRequired();
        }

        var result = await service.UpdateAsync(workItemId, patch, ifMatch, actorId, cancellationToken);
        return Saved(result, response);
    }

    /// <summary>The saved item with its new ETag, or the problem; a stale edit also sends the latest ETag.</summary>
    private static Results<Ok<WorkItemDetail>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult> Saved(
        WorkItemResult result, HttpResponse response)
    {
        if (result is not { Outcome: WorkItemOutcome.Succeeded, Item: { } saved })
        {
            if (result is { Outcome: WorkItemOutcome.Stale, Item: { } latest })
            {
                // US-07 AC1: the client shows the latest version; its ETag is the one to retry with.
                response.Headers.ETag = latest.ETag;
            }

            return Failure(result);
        }

        response.Headers.ETag = saved.ETag;
        return TypedResults.Ok(saved.Item);
    }

    private static ProblemHttpResult IfMatchRequired()
        => TypedResults.Problem(
            title: "If-Match required",
            detail: "Send the ETag from your last read of this item in If-Match.",
            statusCode: StatusCodes.Status428PreconditionRequired);

    private static async Task<Results<Ok<WorkItemDetail>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> ChangeStatusAsync(
        Guid workItemId,
        string? ifMatch,
        ChangeStatusRequest request,
        IValidator<ChangeStatusRequest> validator,
        IWorkItemService service,
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
            return IfMatchRequired();
        }

        var result = await service.ChangeStatusAsync(workItemId, request.To!, ifMatch, actorId, cancellationToken);
        return Saved(result, response);
    }

    private static Results<Ok<WorkItemDetail>, ProblemHttpResult> Found(VersionedWorkItem? item, HttpResponse response)
    {
        // Another organization's item is invisible under RLS, so it is "not found" too.
        if (item is null)
        {
            return TypedResults.Problem(title: "Work item not found", statusCode: StatusCodes.Status404NotFound);
        }

        response.Headers.ETag = item.ETag;
        return TypedResults.Ok(item.Item);
    }

    private static ProblemHttpResult Failure(WorkItemResult result) => result.Outcome switch
    {
        WorkItemOutcome.NotFound => TypedResults.Problem(title: "Not found", statusCode: StatusCodes.Status404NotFound),
        WorkItemOutcome.Stale => TypedResults.Problem(
            title: "Changed by someone else",
            detail: StaleMessage,
            statusCode: StatusCodes.Status412PreconditionFailed),
        WorkItemOutcome.ParentNotAllowed or WorkItemOutcome.ChildrenNotAllowed when result.Violation is { } violation
            => TypedResults.Problem(
                title: result.Outcome == WorkItemOutcome.ParentNotAllowed ? "Parent not allowed" : "Children do not fit the new type",
                detail: violation.Detail,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                extensions: new Dictionary<string, object?>
                {
                    ["allowedParentTypes"] = violation.AllowedParentTypes,
                    ["conflictingChildKeys"] = violation.ConflictingChildKeys,
                }),
        WorkItemOutcome.TransitionNotAllowed when result.Transition is { } transition
            => TypedResults.Problem(
                title: "Status change not allowed",
                detail: transition.Detail,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                extensions: new Dictionary<string, object?> { ["allowedTransitions"] = transition.AllowedTransitions }),
        _ => throw new InvalidOperationException($"No problem response for {result.Outcome}."),
    };

    private static ProblemHttpResult ProjectNotFound()
        => TypedResults.Problem(title: "Project not found", statusCode: StatusCodes.Status404NotFound);
}
