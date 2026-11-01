namespace Trekky.Modules.WorkItems.Domain;

/// <summary>The next item number of a project (US-06 AC2). Numbers only go up, so a key is never reused.</summary>
internal sealed class ItemCounter
{
    /// <summary>Project id from the projects module (no foreign key across modules).</summary>
    public Guid ProjectId { get; init; }

    public Guid OrgId { get; init; }

    public int NextNumber { get; init; }
}
