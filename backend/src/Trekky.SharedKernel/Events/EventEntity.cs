namespace Trekky.SharedKernel.Events;

/// <summary>What an event is about: <c>project</c>, <c>work_item</c>, ... with its human key when it has one.</summary>
public sealed record EventEntity(string Type, Guid Id, string? Key);
