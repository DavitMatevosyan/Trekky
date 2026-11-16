namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>Body of <c>POST /api/v1/work-items/{workItemId}/status</c> (US-10).</summary>
/// <param name="To"><c>todo</c>, <c>in_progress</c>, <c>in_review</c> or <c>done</c>; it must be an allowed move from the current status.</param>
public sealed record ChangeStatusRequest(string? To);
