namespace Trekky.Modules.Activity.Application.Contracts;

/// <summary>A page of history, newest first; <paramref name="NextCursor"/> is <c>null</c> on the last page.</summary>
public sealed record HistoryPage(IReadOnlyList<HistoryEvent> Items, string? NextCursor);
