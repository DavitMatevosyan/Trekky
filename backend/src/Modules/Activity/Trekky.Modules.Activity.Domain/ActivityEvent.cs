namespace Trekky.Modules.Activity.Domain;

/// <summary>
/// One row of the append-only activity log (docs/tech/activity-events.md): who changed what, when. Item history,
/// notifications, webhooks and the AI context layer all read from it.
/// </summary>
internal sealed class ActivityEvent
{
    public const int EventTypeMaxLength = 64;
    public const int ActorTypeMaxLength = 16;
    public const int EntityTypeMaxLength = 32;
    public const int EntityKeyMaxLength = 32;

    public Guid EventId { get; init; }

    public Guid OrgId { get; init; }

    public string EventType { get; init; } = string.Empty;

    public int SchemaVersion { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public string ActorType { get; init; } = string.Empty;

    public Guid? ActorId { get; init; }

    public string EntityType { get; init; } = string.Empty;

    public Guid EntityId { get; init; }

    public string? EntityKey { get; init; }

    public Guid? ProjectId { get; init; }

    /// <summary>The event-specific payload as JSON (jsonb).</summary>
    public string Payload { get; init; } = "{}";
}
