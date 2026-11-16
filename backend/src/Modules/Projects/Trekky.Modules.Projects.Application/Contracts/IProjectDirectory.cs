namespace Trekky.Modules.Projects.Application.Contracts;

/// <summary>
/// Finds projects of the current organization for other modules (ADR-0009: they use only these public contracts).
/// Another organization's project is invisible under RLS, so it is not found either.
/// </summary>
public interface IProjectDirectory
{
    Task<ProjectRef?> FindAsync(Guid projectId, CancellationToken cancellationToken);
}
