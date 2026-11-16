using Trekky.Modules.Projects.Application.Contracts;

namespace Trekky.Modules.Projects.Application.Projects;

/// <summary>A project with its row version, which the API sends as the ETag.</summary>
internal sealed record VersionedProject(Project Project, uint Version)
{
    public string ETag => $"\"{Version}\"";
}
