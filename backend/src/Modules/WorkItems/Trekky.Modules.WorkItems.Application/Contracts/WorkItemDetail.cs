namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>Everything about one work item (US-09 AC1): its fields, its parent and its children (by number).</summary>
/// <param name="AllowedTransitions">The statuses the item can move to now, in workflow order (US-10); drives the status dropdown.</param>
public sealed record WorkItemDetail(
    Guid Id,
    string Key,
    string Type,
    string Title,
    string Status,
    string? ParentKey,
    DateTimeOffset UpdatedAt,
    Guid ProjectId,
    string? DescriptionMd,
    WorkItemSummary? Parent,
    IReadOnlyList<WorkItemSummary> Children,
    IReadOnlyList<string> AllowedTransitions,
    DateTimeOffset CreatedAt);
