namespace Trekky.Modules.WorkItems.Domain;

/// <summary>Activity events the work items module emits (docs/tech/activity-events.md).</summary>
internal static class WorkItemEventTypes
{
    /// <summary>Payload: <c>{ "type", "title", "status", "parentId", "parentKey" }</c>.</summary>
    public const string WorkItemCreated = "WorkItemCreated";

    /// <summary>Payload: <c>{ "changes": { "&lt;field&gt;": { "from": …, "to": … } } }</c> with only the fields that changed.</summary>
    public const string WorkItemUpdated = "WorkItemUpdated";

    /// <summary>Payload: <c>{ "from": "in_progress", "to": "in_review" }</c>.</summary>
    public const string WorkItemStatusChanged = "WorkItemStatusChanged";

    /// <summary>
    /// Payload: <c>{ "from": { "id", "displayName" } | null, "to": … | null }</c>; the names are as they were then, so the
    /// history reads the same after someone renames or leaves.
    /// </summary>
    public const string WorkItemAssigned = "WorkItemAssigned";

    /// <summary>Entity: the work item. Payload: <c>{ "commentId", "excerpt" }</c>, the excerpt being the body's first 200 characters.</summary>
    public const string CommentAdded = "CommentAdded";

    /// <summary>Entity: the work item. Payload: <c>{ "commentId", "excerpt" }</c>, the excerpt being the new body's first 200 characters.</summary>
    public const string CommentEdited = "CommentEdited";

    /// <summary>Entity: the work item. Payload: <c>{ "commentId" }</c>.</summary>
    public const string CommentDeleted = "CommentDeleted";
}
