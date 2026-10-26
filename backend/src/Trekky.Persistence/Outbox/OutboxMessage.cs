using System.Text.Json;
using Trekky.SharedKernel.Events;

namespace Trekky.Persistence.Outbox;

/// <summary>
/// A row of a module's <c>outbox</c> table: an event written in the same transaction as the change it describes,
/// waiting for <see cref="OutboxDispatcher"/> (docs/tech/activity-events.md).
/// </summary>
public sealed class OutboxMessage
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private OutboxMessage()
    {
    }

    /// <summary>Equals the envelope's event id.</summary>
    public Guid Id { get; private set; }

    public Guid OrgId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    /// <summary>The whole <see cref="ActivityEnvelope"/> as JSON (jsonb).</summary>
    public string Envelope { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public static OutboxMessage From(ActivityEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return new OutboxMessage
        {
            Id = envelope.EventId,
            OrgId = envelope.OrgId,
            EventType = envelope.EventType,
            Envelope = JsonSerializer.Serialize(envelope, JsonOptions),
            CreatedAt = envelope.OccurredAt,
        };
    }

    internal static ActivityEnvelope Deserialize(string envelope)
        => JsonSerializer.Deserialize<ActivityEnvelope>(envelope, JsonOptions)
           ?? throw new InvalidOperationException("An outbox row has an empty envelope.");
}
