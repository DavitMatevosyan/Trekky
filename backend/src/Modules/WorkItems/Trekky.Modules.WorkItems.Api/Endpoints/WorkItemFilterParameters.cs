using Microsoft.AspNetCore.Mvc;
using Trekky.Modules.WorkItems.Application.WorkItems;

namespace Trekky.Modules.WorkItems.Api.Endpoints;

/// <summary>
/// The filters of the list and the board as query parameters (TREK-52), with the same names on both endpoints; see
/// <see cref="WorkItemFilter"/> for what each one keeps. Values are read as text, so a wrong one is a 400 naming the
/// parameter rather than a binding failure. No <c>[Description]</c> here: on <c>[AsParameters]</c> it breaks the
/// OpenAPI document (InvalidCastException in .NET 10), so the endpoints describe the filters instead.
/// </summary>
internal sealed record WorkItemFilterParameters(
    [FromQuery(Name = "open")] string? Open,
    [FromQuery(Name = "status")] string[]? Status,
    [FromQuery(Name = "type")] string[]? Type,
    [FromQuery(Name = "assignee")] string[]? Assignee,
    [FromQuery(Name = "parent")] string? Parent,
    [FromQuery(Name = "q")] string? Q,
    [FromQuery(Name = "completedEpics")] string? CompletedEpics)
{
    public WorkItemFilter ToFilter(Guid currentUserId)
        => WorkItemFilter.From(Type, Q, Status, Open, Assignee, Parent, CompletedEpics, currentUserId);
}
