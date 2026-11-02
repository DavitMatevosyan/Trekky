namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>Why a parent change was refused, with what would be allowed instead (US-06 AC5, US-07 AC2).</summary>
/// <param name="AllowedParentTypes">The parent types the item's type allows.</param>
/// <param name="ConflictingChildKeys">Children whose type does not allow the item's new type as their parent.</param>
internal sealed record RuleViolation(string Detail, IReadOnlyList<string> AllowedParentTypes, IReadOnlyList<string> ConflictingChildKeys);
