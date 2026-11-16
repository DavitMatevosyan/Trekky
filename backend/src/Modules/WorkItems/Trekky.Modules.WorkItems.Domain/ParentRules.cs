namespace Trekky.Modules.WorkItems.Domain;

/// <summary>
/// Which item types may be the parent of which (US-06 AC4). Every allowed parent sits higher in the hierarchy than
/// its child, so following parents can never loop back to an item.
/// </summary>
internal static class ParentRules
{
    private static readonly Dictionary<string, string[]> AllowedParents = new(StringComparer.Ordinal)
    {
        [WorkItemTypes.Epic] = [],
        [WorkItemTypes.Story] = [WorkItemTypes.Epic],
        [WorkItemTypes.Task] = [WorkItemTypes.Epic, WorkItemTypes.Story],
        [WorkItemTypes.SubTask] = [WorkItemTypes.Story, WorkItemTypes.Task],
        [WorkItemTypes.Bug] = [WorkItemTypes.Epic, WorkItemTypes.Story],
        [WorkItemTypes.TechDebt] = [WorkItemTypes.Epic],
    };

    /// <summary>The types an item of <paramref name="type"/> may have as its parent, top of the hierarchy first.</summary>
    public static IReadOnlyList<string> AllowedParentTypes(string type) => AllowedParents[type];

    /// <summary>A SubTask always needs a parent; every other type may stand alone.</summary>
    public static bool RequiresParent(string type) => type == WorkItemTypes.SubTask;

    /// <summary>Whether an item of <paramref name="type"/> may have a parent of <paramref name="parentType"/> (<c>null</c>: no parent).</summary>
    public static bool Allows(string type, string? parentType)
        => parentType is null ? !RequiresParent(type) : AllowedParents[type].Contains(parentType, StringComparer.Ordinal);
}
