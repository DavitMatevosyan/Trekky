namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>Why a status change was refused, with the statuses the item can move to instead (US-10 AC3).</summary>
internal sealed record TransitionViolation(string Detail, IReadOnlyList<string> AllowedTransitions);
