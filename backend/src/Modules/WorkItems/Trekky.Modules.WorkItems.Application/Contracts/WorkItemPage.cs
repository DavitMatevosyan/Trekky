namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>
/// A page of work items, newest update first; pass <see cref="NextCursor"/> back as <c>cursor</c> for the next one.
/// <see cref="TotalCount"/> is how many items match the filters across every page (TREK-52).
/// </summary>
public sealed record WorkItemPage(IReadOnlyList<WorkItemSummary> Items, string? NextCursor, int TotalCount);
