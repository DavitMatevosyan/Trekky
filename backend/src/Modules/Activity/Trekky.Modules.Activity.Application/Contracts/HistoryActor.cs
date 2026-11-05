namespace Trekky.Modules.Activity.Application.Contracts;

/// <summary>Who caused an event: <c>user</c>, <c>agent</c> or <c>system</c>.</summary>
/// <param name="DisplayName">The user's name now; <c>null</c> for <c>system</c> or someone no longer in the organization.</param>
public sealed record HistoryActor(string Type, Guid? Id, string? DisplayName);
