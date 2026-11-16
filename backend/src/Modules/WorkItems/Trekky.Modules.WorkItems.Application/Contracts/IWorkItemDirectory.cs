namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>
/// Finds work items of the current organization for other modules (ADR-0009: they use only these public contracts).
/// Another organization's item is invisible under RLS, so it is not found either.
/// </summary>
public interface IWorkItemDirectory
{
    Task<bool> ExistsAsync(Guid workItemId, CancellationToken cancellationToken);
}
