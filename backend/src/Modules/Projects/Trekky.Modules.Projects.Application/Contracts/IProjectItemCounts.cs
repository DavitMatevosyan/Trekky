namespace Trekky.Modules.Projects.Application.Contracts;

/// <summary>
/// Counts the work items of projects (US-05 AC3). The work items module implements it, so projects never read
/// another module's tables and the two modules depend on each other in one direction only.
/// </summary>
public interface IProjectItemCounts
{
    /// <summary>Item count per project id; projects without items may be missing from the result.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);
}
