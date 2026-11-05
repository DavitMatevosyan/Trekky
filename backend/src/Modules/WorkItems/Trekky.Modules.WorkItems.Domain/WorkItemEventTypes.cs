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
}
