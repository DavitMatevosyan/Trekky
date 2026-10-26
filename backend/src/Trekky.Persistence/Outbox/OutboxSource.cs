namespace Trekky.Persistence.Outbox;

/// <summary>A module schema with an <c>outbox</c> table for <see cref="OutboxDispatcher"/> to drain.</summary>
internal sealed record OutboxSource(string Schema);
