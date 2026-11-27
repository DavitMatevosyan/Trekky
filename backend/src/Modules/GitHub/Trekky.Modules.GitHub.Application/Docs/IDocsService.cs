using Trekky.Modules.GitHub.Application.Contracts;

namespace Trekky.Modules.GitHub.Application.Docs;

/// <summary>
/// A project's docs, read from its linked GitHub repository and cached per organization by blob SHA (TREK-75,
/// ADR-0030). The cache is refreshed when it is opened after <see cref="StaleAfter"/>, or on request; when GitHub cannot
/// be reached, the cached copy is served and marked stale.
/// </summary>
internal interface IDocsService
{
    /// <summary>How long a cached folder is shown before GitHub is asked again.</summary>
    static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    Task<DocsResult<DocsTree>> GetTreeAsync(Guid projectId, bool refresh, CancellationToken cancellationToken);

    /// <summary>The doc at <paramref name="path"/> (inside the docs folder).</summary>
    Task<DocsResult<DocContent>> GetDocAsync(Guid projectId, string? path, CancellationToken cancellationToken);

    /// <summary>An image at <paramref name="path"/> (inside the docs folder), up to 5 MB.</summary>
    Task<DocsResult<DocRawFile>> GetImageAsync(Guid projectId, string? path, CancellationToken cancellationToken);

    /// <summary>Full-text search across the cached docs; words match by prefix.</summary>
    Task<DocsResult<DocSearchResults>> SearchAsync(Guid projectId, string query, CancellationToken cancellationToken);
}
