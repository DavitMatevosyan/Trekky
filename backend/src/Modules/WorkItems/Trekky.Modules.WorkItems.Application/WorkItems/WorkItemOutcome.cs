namespace Trekky.Modules.WorkItems.Application.WorkItems;

internal enum WorkItemOutcome
{
    Succeeded,

    /// <summary>The item or project does not exist, or belongs to another organization.</summary>
    NotFound,

    /// <summary>The parent is missing, in another project or of a type this item may not have (US-06 AC4, AC5).</summary>
    ParentNotAllowed,

    /// <summary>A new type would no longer fit as the parent of some of the item's children (US-07 AC2).</summary>
    ChildrenNotAllowed,

    /// <summary>The workflow does not allow moving from the current status to the requested one (US-10 AC2, AC3).</summary>
    TransitionNotAllowed,

    /// <summary><c>If-Match</c> does not match the current version (US-07 AC1).</summary>
    Stale,
}
