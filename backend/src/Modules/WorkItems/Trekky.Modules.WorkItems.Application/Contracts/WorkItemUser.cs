namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>A user as shown on a work item, e.g. who created it. Owned here so the item's contract does not depend on the identity module's.</summary>
public sealed record WorkItemUser(Guid Id, string DisplayName, string? Email);
