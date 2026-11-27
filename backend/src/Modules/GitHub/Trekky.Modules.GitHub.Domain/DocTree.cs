namespace Trekky.Modules.GitHub.Domain;

/// <summary>
/// The cached state of a project's docs folder (TREK-75, ADR-0030): which commit of the linked branch the cached
/// <see cref="DocFile"/> rows were read from and when GitHub was last asked. One per <see cref="ProjectRepository"/>;
/// deleted with it, and when the link's repository, branch or folder changes.
/// </summary>
internal sealed class DocTree
{
    public Guid Id { get; init; }

    public Guid OrgId { get; init; }

    /// <summary>The <see cref="ProjectRepository"/> link this cache belongs to.</summary>
    public Guid LinkId { get; init; }

    public Guid ProjectId { get; init; }

    /// <summary>
    /// The repository id, branch and docs folder the cache was read from (<see cref="SourceOf"/>). When the project's
    /// link is changed to another one, the cached files are dropped and read again.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>The branch head the files were read at; <c>null</c> until the first successful read.</summary>
    public string? CommitSha { get; set; }

    /// <summary>The head commit's message (first line), author and date, shown as the folder's last change.</summary>
    public string? CommitMessage { get; set; }

    public string? CommitAuthor { get; set; }

    public DateTimeOffset? CommittedAt { get; set; }

    /// <summary>When GitHub was last asked for the branch head; the cache is refreshed on open when this is old.</summary>
    public DateTimeOffset CheckedAt { get; set; }

    /// <summary>The docs folder no longer exists on the branch (deleted or renamed on GitHub).</summary>
    public bool FolderMissing { get; set; }

    /// <summary>Not every file was cached: the folder had more than <see cref="DocFile.MaxFiles"/> Markdown files, or GitHub cut the listing short.</summary>
    public bool Truncated { get; set; }

    public static string SourceOf(ProjectRepository link)
    {
        ArgumentNullException.ThrowIfNull(link);
        return $"{link.RepositoryId}:{link.Branch}:{link.DocsPath}";
    }
}
