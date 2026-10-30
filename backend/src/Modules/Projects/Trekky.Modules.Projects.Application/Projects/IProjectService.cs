using Trekky.Modules.Projects.Application.Contracts;

namespace Trekky.Modules.Projects.Application.Projects;

/// <summary>Projects of the current organization (the request's tenant).</summary>
internal interface IProjectService
{
    /// <summary>Creates the project and its <c>ProjectCreated</c> event in one transaction. Input must be validated.</summary>
    Task<(CreateProjectOutcome Outcome, VersionedProject? Project)> CreateAsync(
        CreateProjectRequest request, Guid actorId, CancellationToken cancellationToken);

    Task<ProjectPage> ListAsync(int limit, ProjectCursor? after, CancellationToken cancellationToken);

    /// <summary>The project, or <c>null</c> when it does not exist or belongs to another organization.</summary>
    Task<VersionedProject?> GetAsync(Guid projectId, CancellationToken cancellationToken);
}
