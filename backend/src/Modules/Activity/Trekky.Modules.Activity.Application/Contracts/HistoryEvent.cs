using System.Text.Json;

namespace Trekky.Modules.Activity.Application.Contracts;

/// <summary>
/// One activity event in an item's history (US-09): the stored event (docs/tech/activity-events.md) with the actor's
/// display name, so a client can write "Davit changed status In Progress → In Review" without more calls.
/// </summary>
/// <param name="Payload">Event-specific, as stored; its shape depends on <paramref name="EventType"/> and <paramref name="SchemaVersion"/>.</param>
public sealed record HistoryEvent(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    HistoryActor Actor,
    HistoryEntity Entity,
    JsonElement Payload);
