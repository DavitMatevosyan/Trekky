namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>A work item in a list (US-08): key, type, title, status, parent key, last update and assignee (US-13).</summary>
/// <param name="Assignee">The member who owns it; <c>null</c> for nobody, or when they are no longer a member.</param>
public sealed record WorkItemSummary(
    Guid Id, string Key, string Type, string Title, string Status, string? ParentKey, DateTimeOffset UpdatedAt, WorkItemUser? Assignee);
