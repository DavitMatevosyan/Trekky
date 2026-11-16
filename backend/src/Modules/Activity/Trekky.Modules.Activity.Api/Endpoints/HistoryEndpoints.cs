using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Trekky.Modules.Activity.Application.Contracts;
using Trekky.Modules.Activity.Application.History;

namespace Trekky.Modules.Activity.Api.Endpoints;

/// <summary>An item's history from the activity log, under /api/v1 (OpenAPI draft, TRK-5; US-09).</summary>
internal static class HistoryEndpoints
{
    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/work-items/{workItemId:guid}/history", (Guid workItemId, int? limit, string? cursor,
                    IHistoryService service, CancellationToken cancellationToken)
                => GetWorkItemHistoryAsync(workItemId, limit, cursor, service, cancellationToken))
            .WithTags("WorkItems")
            .WithName("GetWorkItemHistory")
            .WithSummary("An item's activity events, newest first (US-09)")
            .WithDescription(
                "Every recorded change to the item, with the actor's display name. A change can take up to a second to appear (the outbox dispatcher).")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Ok<HistoryPage>, ValidationProblem, ProblemHttpResult>> GetWorkItemHistoryAsync(
        Guid workItemId, int? limit, string? cursor, IHistoryService service, CancellationToken cancellationToken)
    {
        // Every query-string problem in one answer.
        var errors = new Dictionary<string, string[]>();
        var pageSize = limit ?? HistoryCursor.DefaultLimit;
        if (pageSize is < 1 or > HistoryCursor.MaxLimit)
        {
            errors["limit"] = [$"Limit must be between 1 and {HistoryCursor.MaxLimit}."];
        }

        if (!HistoryCursor.TryDecode(cursor, out var after))
        {
            errors["cursor"] = ["The cursor is not valid; use nextCursor from the previous page."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var page = await service.GetWorkItemHistoryAsync(workItemId, pageSize, after, cancellationToken);
        return page is null
            ? TypedResults.Problem(title: "Work item not found", statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(page);
    }
}
