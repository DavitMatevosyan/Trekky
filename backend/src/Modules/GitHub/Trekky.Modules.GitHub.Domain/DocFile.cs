namespace Trekky.Modules.GitHub.Domain;

/// <summary>
/// One Markdown file of a project's docs folder, cached by its GitHub blob SHA (TREK-75, ADR-0033). Its text is
/// searchable through the table's generated <c>search</c> column (Postgres full-text search).
/// </summary>
internal sealed class DocFile
{
    /// <summary>Files cached per project; a folder with more is shown and searched in part (<see cref="DocTree.Truncated"/>).</summary>
    public const int MaxFiles = 1000;

    /// <summary>Larger files are listed but not cached, rendered or searched (GitHub's blob API serves up to 100 MB).</summary>
    public const int MaxBytes = 1024 * 1024;

    public const int PathMaxLength = 1024;

    public const int TitleMaxLength = 300;

    public Guid Id { get; init; }

    public Guid OrgId { get; init; }

    public Guid TreeId { get; init; }

    /// <summary>The file's path inside the docs folder, e.g. <c>adr/0001-modular-monolith.md</c>.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>GitHub's blob SHA of the cached content: the cache key and, later, the edit conflict check.</summary>
    public string BlobSha { get; set; } = string.Empty;

    /// <summary>The first <c>#</c> heading, or the file name without <c>.md</c>.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The UTF-8 text; <c>null</c> when the file is larger than <see cref="MaxBytes"/>.</summary>
    public string? Content { get; set; }

    public int Size { get; set; }

    /// <summary>The last commit that changed this file, read when the file is first opened at this blob SHA.</summary>
    public string? CommitSha { get; set; }

    public string? CommitMessage { get; set; }

    public string? CommitAuthor { get; set; }

    public DateTimeOffset? CommittedAt { get; set; }
}
