namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>A work item in a list (US-08): key, type, title, status, parent key and last update.</summary>
public sealed record WorkItemSummary(
    Guid Id, string Key, string Type, string Title, string Status, string? ParentKey, DateTimeOffset UpdatedAt);
