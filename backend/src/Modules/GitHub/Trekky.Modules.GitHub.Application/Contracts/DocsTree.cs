namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>A project's docs folder as Trekky has it cached (TREK-75, ADR-0033).</summary>
/// <param name="Repository"><c>owner/name</c>.</param>
/// <param name="DocsPath">The docs folder with a trailing <c>/</c>, or empty for the repository root.</param>
/// <param name="HtmlUrl">The docs folder on GitHub.</param>
/// <param name="LastCommit">The branch's latest commit when the files were read; <c>null</c> before the first read.</param>
/// <param name="CheckedAt">When GitHub was last asked; <c>null</c> when it never answered.</param>
/// <param name="Stale">GitHub could not be reached just now, so this is the last cached copy.</param>
/// <param name="FolderMissing">The branch or docs folder no longer exists on GitHub.</param>
/// <param name="Truncated">The folder has more Markdown files than Trekky caches (1,000); the rest are not listed.</param>
/// <param name="Files">The Markdown files, by path.</param>
public sealed record DocsTree(
    Guid ProjectId,
    string Repository,
    string Branch,
    string DocsPath,
    string HtmlUrl,
    DocCommit? LastCommit,
    DateTimeOffset? CheckedAt,
    bool Stale,
    bool FolderMissing,
    bool Truncated,
    IReadOnlyList<DocListing> Files);
