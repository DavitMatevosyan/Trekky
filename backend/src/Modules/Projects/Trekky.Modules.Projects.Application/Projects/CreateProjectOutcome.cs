namespace Trekky.Modules.Projects.Application.Projects;

internal enum CreateProjectOutcome
{
    Created,

    /// <summary>The key is already used in this organization (US-05 AC1).</summary>
    KeyTaken,
}
