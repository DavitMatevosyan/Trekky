namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>Everything about one work item (US-09 AC1): its fields, its parent and its children (by number).</summary>
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
    DateTimeOffset CreatedAt);
