namespace Trekky.Modules.Activity.Application.Contracts;

/// <summary>What an event is about: <c>project</c>, <c>work_item</c>, ... with its human key when it has one.</summary>
public sealed record HistoryEntity(string Type, Guid Id, string? Key);
