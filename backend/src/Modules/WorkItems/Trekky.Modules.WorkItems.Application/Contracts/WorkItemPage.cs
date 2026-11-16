namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>A page of work items, newest update first; pass <see cref="NextCursor"/> back as <c>cursor</c> for the next one.</summary>
public sealed record WorkItemPage(IReadOnlyList<WorkItemSummary> Items, string? NextCursor);
