namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>A card on the board (US-11, US-13). <see cref="Etag"/> goes in <c>If-Match</c> when the card is moved.</summary>
public sealed record BoardCard(
    Guid Id,
    string Key,
    string Type,
    string Title,
    string Status,
    string? ParentKey,
    DateTimeOffset UpdatedAt,
    string Etag,
    WorkItemUser? Assignee);
