namespace Trekky.Modules.GitHub.Domain;

/// <summary>
/// Where a project's documentation lives on GitHub (TREK-74): one repository of a connected installation, a folder in
/// it and the branch Trekky reads and opens pull requests against. A project has at most one.
/// </summary>
internal sealed class ProjectRepository
{
    /// <summary><c>owner/name</c>: owner up to 39 characters, name up to 100.</summary>
    public const int RepositoryMaxLength = 140;

    /// <summary>Owner (letters, digits, hyphens) and name (letters, digits, <c>.</c>, <c>_</c>, <c>-</c>), as GitHub allows them.</summary>
    public const string RepositoryRegex = @"\A[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9._-]{1,100}\z";

    public const string DefaultDocsPath = "docs/";

    public const string DefaultBranch = "dev";

    public Guid Id { get; init; }

    public Guid OrgId { get; init; }

    /// <summary>The project, from the projects module (no foreign key across modules).</summary>
    public Guid ProjectId { get; init; }

    /// <summary>The <see cref="GitHubInstallation"/> row whose tokens reach the repository.</summary>
    public Guid InstallationRowId { get; set; }

    /// <summary>GitHub's repository id, which survives renames.</summary>
    public long RepositoryId { get; set; }

    /// <summary><c>owner/name</c> as GitHub returned it when the link was saved.</summary>
    public string RepositoryFullName { get; set; } = string.Empty;

    /// <summary>The docs folder, normalized by <see cref="DocsPath"/>: <c>docs/</c>, or empty for the repository root.</summary>
    public string DocsPath { get; set; } = DefaultDocsPath;

    public string Branch { get; set; } = DefaultBranch;

    /// <summary>User id of whoever saved the current settings.</summary>
    public Guid LinkedBy { get; set; }

    public DateTimeOffset LinkedAt { get; set; }

    /// <summary>Postgres <c>xmin</c>; the ETag for edits.</summary>
    public uint Version { get; init; }
}
