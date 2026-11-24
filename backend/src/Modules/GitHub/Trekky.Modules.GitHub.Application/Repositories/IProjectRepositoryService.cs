using Trekky.Modules.GitHub.Application.Contracts;

namespace Trekky.Modules.GitHub.Application.Repositories;

/// <summary>Where each project of the current organization keeps its docs on GitHub (TREK-74).</summary>
internal interface IProjectRepositoryService
{
    /// <summary>The project's link, or <c>null</c> when it has none or the project is not the organization's.</summary>
    Task<VersionedProjectRepositoryLink?> GetAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates or replaces the project's link after checking on GitHub that the installation reaches the repository and
    /// that the branch and docs folder exist, with its event in one transaction. Replacing needs a matching
    /// <paramref name="ifMatch"/>. Input must be validated.
    /// </summary>
    Task<LinkRepositoryResult> LinkAsync(
        Guid projectId, LinkProjectRepositoryRequest request, string? ifMatch, Guid actorId, CancellationToken cancellationToken);

    /// <summary>Removes the project's link; <c>false</c> when there was none.</summary>
    Task<bool> UnlinkAsync(Guid projectId, Guid actorId, CancellationToken cancellationToken);
}
