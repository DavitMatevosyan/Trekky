namespace Trekky.Modules.Projects.Domain;

/// <summary>Activity events the projects module emits (docs/tech/activity-events.md).</summary>
internal static class ProjectEventTypes
{
    /// <summary>Payload: <c>{ "key": "TRK", "name": "Trekky" }</c>.</summary>
    public const string ProjectCreated = "ProjectCreated";
}
