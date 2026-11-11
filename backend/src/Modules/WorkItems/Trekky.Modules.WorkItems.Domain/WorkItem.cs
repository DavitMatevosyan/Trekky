namespace Trekky.Modules.WorkItems.Domain;

/// <summary>
/// One work item of any type (US-06), in one table with <see cref="ParentId"/> for the hierarchy. The key
/// (<see cref="ProjectKey"/> and <see cref="Number"/>) never changes.
/// </summary>
internal sealed class WorkItem
{
    public const int TitleMaxLength = 200;

    public const int DescriptionMaxLength = 50_000;

    public const string EntityType = "work_item";

    public Guid Id { get; init; }

    public Guid OrgId { get; init; }

    /// <summary>Project id from the projects module (no foreign key across modules).</summary>
    public Guid ProjectId { get; init; }

    /// <summary>Copied from the project at creation; project keys never change.</summary>
    public string ProjectKey { get; init; } = string.Empty;

    public int Number { get; init; }

    public string Type { get; set; } = WorkItemTypes.Task;

    public string Title { get; set; } = string.Empty;

    public string? DescriptionMd { get; set; }

    public string Status { get; set; } = WorkItemStatuses.ToDo;

    /// <summary>An item in the same project; <see cref="ParentRules"/> decides which types may be parents.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Position within its status column on the board (US-11 AC4), a <see cref="BoardRank"/>; unique per project and
    /// status. Changing it alone is not an activity event (docs/tech/activity-events.md).
    /// </summary>
    public required string Rank { get; set; }

    /// <summary>Reserved for custom fields (v1.5): a JSON object, <c>{}</c> until then.</summary>
    public string Custom { get; init; } = "{}";

    /// <summary>User id from the identity module (no foreign key across modules).</summary>
    public Guid CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When the item last moved to Done; <c>null</c> in any other status. Orders the board's Done column.</summary>
    public DateTimeOffset? DoneAt { get; set; }

    /// <summary>Postgres <c>xmin</c>; sent to clients as the ETag and checked against <c>If-Match</c> on edits.</summary>
    public uint Version { get; init; }

    public string Key => WorkItemKey.Format(ProjectKey, Number);
}
