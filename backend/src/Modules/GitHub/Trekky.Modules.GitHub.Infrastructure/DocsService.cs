using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Trekky.Modules.GitHub.Application.Contracts;
using Trekky.Modules.GitHub.Application.Docs;
using Trekky.Modules.GitHub.Application.GitHub;
using Trekky.Modules.GitHub.Domain;
using Trekky.Modules.GitHub.Persistence;
using Trekky.SharedKernel;

namespace Trekky.Modules.GitHub.Infrastructure;

/// <summary>
/// The docs viewer's reads (TREK-75, ADR-0033). A refresh asks GitHub for the branch head; only when it moved does it
/// read the docs folder's tree, and only files whose blob SHA changed are fetched again. Writes happen in one short
/// transaction after GitHub has answered, so no transaction waits on GitHub.
/// </summary>
internal sealed partial class DocsService(
    GitHubDbContext db,
    ITenantContext tenant,
    TimeProvider time,
    IGitHubApp app,
    IGitHubClient github,
    ILogger<DocsService> logger) : IDocsService
{
    /// <summary>Images larger than this are not served.</summary>
    public const int MaxImageBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Blobs fetched in one refresh. A large folder is cached over several opens; a doc that is not cached yet is
    /// fetched when it is opened.
    /// </summary>
    public const int MaxBlobsPerRefresh = 100;

    public const int MaxSearchResults = 20;

    /// <summary>Words of a query used; the rest are ignored.</summary>
    public const int MaxSearchTerms = 10;

    private const int BlobFetchConcurrency = 4;

    /// <summary>Private-use characters that mark matches in <c>ts_headline</c>'s output; they never occur in snippets otherwise.</summary>
    private const char MatchStart = '\uE000';

    private const char MatchEnd = '\uE001';

    private static readonly string HeadlineOptions =
        $"StartSel={MatchStart}, StopSel={MatchEnd}, MaxWords=30, MinWords=12, MaxFragments=2, FragmentDelimiter=\" … \"";

    public async Task<DocsResult<DocsTree>> GetTreeAsync(Guid projectId, bool refresh, CancellationToken cancellationToken)
    {
        var (linked, outcome) = await FindLinkAsync(projectId, cancellationToken);
        if (linked is null)
        {
            return new(outcome);
        }

        var cache = await EnsureFreshAsync(linked, refresh, cancellationToken);
        if (cache.Tree is null)
        {
            return new(cache.Outcome);
        }

        var files = await db.DocFiles.AsNoTracking()
            .Where(f => f.TreeId == cache.Tree.Id)
            .OrderBy(f => f.Path)
            .Select(f => new DocListing(f.Path, f.Title, f.Size, f.Size > DocFile.MaxBytes))
            .ToListAsync(cancellationToken);

        // Postgres sorts by the database collation; the tree is shown in plain code-point order everywhere.
        files.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));

        var link = linked.Link;
        var tree = cache.Tree;
        return DocsResult<DocsTree>.Ok(new DocsTree(
            link.ProjectId,
            link.RepositoryFullName,
            link.Branch,
            link.DocsPath,
            GitHubUrls.Folder(link.RepositoryFullName, link.Branch, link.DocsPath),
            ToCommit(link, tree.CommitSha, tree.CommitMessage, tree.CommitAuthor, tree.CommittedAt),
            tree.CommitSha is null && !tree.FolderMissing ? null : tree.CheckedAt,
            cache.Stale,
            tree.FolderMissing,
            tree.Truncated,
            files));
    }

    public async Task<DocsResult<DocContent>> GetDocAsync(Guid projectId, string? path, CancellationToken cancellationToken)
    {
        var docPath = DocPath.Normalize(path);
        if (docPath is null || !DocPath.IsMarkdown(docPath))
        {
            return new(DocsOutcome.InvalidPath);
        }

        var (linked, outcome) = await FindLinkAsync(projectId, cancellationToken);
        if (linked is null)
        {
            return new(outcome);
        }

        var cache = await EnsureFreshAsync(linked, force: false, cancellationToken);
        if (cache.Tree is null)
        {
            return new(cache.Outcome);
        }

        var file = await db.DocFiles.FirstOrDefaultAsync(f => f.TreeId == cache.Tree.Id && f.Path == docPath, cancellationToken);
        if (file is null)
        {
            return new(DocsOutcome.NotFound);
        }

        var link = linked.Link;
        var installationId = linked.Installation.InstallationId;
        var stale = cache.Stale;
        var tooLarge = file.Size > DocFile.MaxBytes;
        if (!stale && ((file.Content is null && !tooLarge) || file.CommitSha is null))
        {
            try
            {
                if (file.Content is null && !tooLarge)
                {
                    var bytes = await github.GetBlobAsync(installationId, link.RepositoryFullName, file.BlobSha, cancellationToken);
                    file.Content = Decode(bytes);
                    file.Title = DocPath.TitleOf(file.Path, file.Content);
                }

                if (file.CommitSha is null)
                {
                    var commit = await github.GetLastCommitAsync(
                        installationId, link.RepositoryFullName, link.Branch, DocPath.InRepository(link.DocsPath, file.Path), cancellationToken);
                    if (commit is not null)
                    {
                        file.CommitSha = commit.Sha;
                        file.CommitMessage = Truncate(commit.Message, DocFile.TitleMaxLength);
                        file.CommitAuthor = Truncate(commit.Author, 200);
                        file.CommittedAt = commit.CommittedAt;
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
            }
            catch (GitHubInstallationGoneException)
            {
                return new(DocsOutcome.InstallationGone);
            }
            catch (GitHubUnavailableException exception)
            {
                logger.LogWarning(exception, "Reading doc {Path} of project {ProjectId} from GitHub failed; serving the cache.", docPath, projectId);
                stale = true;
            }
            catch (DbUpdateConcurrencyException)
            {
                // A refresh replaced the row meanwhile; what was read is still this version of the file.
                db.ChangeTracker.Clear();
            }
        }

        if (file.Content is null && !tooLarge)
        {
            return new(DocsOutcome.Unavailable);
        }

        return DocsResult<DocContent>.Ok(new DocContent(
            file.Path,
            file.Title,
            file.Content,
            file.BlobSha,
            file.Size,
            tooLarge,
            ToCommit(link, file.CommitSha, file.CommitMessage, file.CommitAuthor, file.CommittedAt),
            GitHubUrls.File(link.RepositoryFullName, link.Branch, DocPath.InRepository(link.DocsPath, file.Path)),
            stale));
    }

    public async Task<DocsResult<DocRawFile>> GetImageAsync(Guid projectId, string? path, CancellationToken cancellationToken)
    {
        var docPath = DocPath.Normalize(path);
        var contentType = docPath is null ? null : DocPath.ImageContentType(docPath);
        if (docPath is null || contentType is null)
        {
            return new(DocsOutcome.InvalidPath);
        }

        var (linked, outcome) = await FindLinkAsync(projectId, cancellationToken);
        if (linked is null)
        {
            return new(outcome);
        }

        // Images are read at the commit the cached docs come from; they never start a refresh of their own.
        var source = DocTree.SourceOf(linked.Link);
        var cachedTree = await db.DocTrees.AsNoTracking()
            .FirstOrDefaultAsync(t => t.LinkId == linked.Link.Id && t.Source == source && t.CommitSha != null, cancellationToken);
        var cache = cachedTree is not null
            ? new CacheState(cachedTree, Stale: false, DocsOutcome.Ok)
            : await EnsureFreshAsync(linked, force: false, cancellationToken);
        if (cache.Tree is null)
        {
            return new(cache.Outcome);
        }

        if (cache.Tree.CommitSha is null || cache.Tree.FolderMissing)
        {
            return new(DocsOutcome.NotFound);
        }

        try
        {
            // Read at the cached commit, so images match the docs shown with them.
            var bytes = await github.GetFileAsync(
                linked.Installation.InstallationId,
                linked.Link.RepositoryFullName,
                cache.Tree.CommitSha,
                DocPath.InRepository(linked.Link.DocsPath, docPath),
                MaxImageBytes,
                cancellationToken);
            return bytes is null ? new(DocsOutcome.NotFound) : DocsResult<DocRawFile>.Ok(new DocRawFile(bytes, contentType));
        }
        catch (GitHubInstallationGoneException)
        {
            return new(DocsOutcome.InstallationGone);
        }
    }

    public async Task<DocsResult<DocSearchResults>> SearchAsync(Guid projectId, string query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (linked, outcome) = await FindLinkAsync(projectId, cancellationToken);
        if (linked is null)
        {
            return new(outcome);
        }

        var cache = await EnsureFreshAsync(linked, force: false, cancellationToken);
        if (cache.Tree is null)
        {
            return new(cache.Outcome);
        }

        var tsQuery = ToPrefixQuery(query);
        var incomplete = cache.Tree.Truncated
            || await db.DocFiles.AnyAsync(f => f.TreeId == cache.Tree.Id && f.Content == null && f.Size <= DocFile.MaxBytes, cancellationToken);
        if (tsQuery is null || cache.Tree.FolderMissing)
        {
            return DocsResult<DocSearchResults>.Ok(new DocSearchResults(query, [], incomplete));
        }

        var configuration = GitHubDbContext.SearchConfiguration;
        var treeId = cache.Tree.Id;
        var rows = await db.Database.SqlQuery<SearchRow>($"""
            SELECT f.path AS "Path", f.title AS "Title",
                   ts_headline({configuration}::regconfig, coalesce(f.content, ''), q, {HeadlineOptions}) AS "Snippet"
            FROM github.doc_files f, to_tsquery({configuration}::regconfig, {tsQuery}) q
            WHERE f.tree_id = {treeId} AND f.search @@ q
            ORDER BY ts_rank(f.search, q) DESC, f.path
            LIMIT {MaxSearchResults}
            """).ToListAsync(cancellationToken);

        var hits = rows.Select(r => new DocSearchHit(r.Path, r.Title, ToSnippet(r.Snippet))).ToList();
        return DocsResult<DocSearchResults>.Ok(new DocSearchResults(query, hits, incomplete));
    }

    /// <summary>
    /// The query as a Postgres <c>tsquery</c> in which every word must match, by prefix (<c>dock:* &amp; comp:*</c>);
    /// <c>null</c> when it has no words. Only letters, digits and <c>_</c> make words, so nothing of the user's text is
    /// read as <c>tsquery</c> syntax.
    /// </summary>
    internal static string? ToPrefixQuery(string query)
    {
        var terms = Words().Matches(query)
            .Select(m => m.Value.Length > 50 ? m.Value[..50] : m.Value)
            .Select(w => w.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxSearchTerms)
            .ToList();
        return terms.Count == 0 ? null : string.Join(" & ", terms.Select(t => t + ":*"));
    }

    /// <summary><c>ts_headline</c>'s output split into plain and matched runs, with whitespace collapsed.</summary>
    internal static IReadOnlyList<DocSnippetPart> ToSnippet(string headline)
    {
        var parts = new List<DocSnippetPart>();
        var text = Whitespace().Replace(headline, " ").Trim();
        var current = new StringBuilder();
        var inMatch = false;
        foreach (var c in text)
        {
            if (c is MatchStart or MatchEnd)
            {
                if (current.Length > 0)
                {
                    parts.Add(new DocSnippetPart(current.ToString(), inMatch));
                    current.Clear();
                }

                inMatch = c == MatchStart;
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            parts.Add(new DocSnippetPart(current.ToString(), inMatch));
        }

        return parts;
    }

    /// <summary>The file's text: UTF-8 without a byte order mark, and without NUL characters (Postgres text cannot hold them).</summary>
    internal static string Decode(byte[] bytes)
        => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(bytes).TrimStart('\uFEFF').Replace("\0", string.Empty, StringComparison.Ordinal);

    private async Task<(LinkedRepository? Linked, DocsOutcome Outcome)> FindLinkAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var row = await db.ProjectRepositories.AsNoTracking()
            .Where(l => l.ProjectId == projectId)
            .Join(db.Installations.AsNoTracking(), l => l.InstallationRowId, i => i.Id, (link, installation) => new LinkedRepository(link, installation))
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return (null, DocsOutcome.NotLinked);
        }

        return app.IsConfigured ? (row, DocsOutcome.Ok) : (null, DocsOutcome.NotConfigured);
    }

    /// <summary>
    /// The project's cache, refreshed first when it is older than <see cref="IDocsService.StaleAfter"/>, was read for
    /// other link settings, or <paramref name="force"/> is set. When GitHub cannot be reached the last cache is
    /// returned marked stale; with nothing cached the outcome says why.
    /// </summary>
    private async Task<CacheState> EnsureFreshAsync(LinkedRepository linked, bool force, CancellationToken cancellationToken)
    {
        var source = DocTree.SourceOf(linked.Link);
        var tree = await db.DocTrees.FirstOrDefaultAsync(t => t.LinkId == linked.Link.Id, cancellationToken);
        var read = tree is not null && tree.Source == source && (tree.CommitSha is not null || tree.FolderMissing);
        if (read && !force && time.GetUtcNow() - tree!.CheckedAt < IDocsService.StaleAfter)
        {
            return new CacheState(tree, Stale: false, DocsOutcome.Ok);
        }

        try
        {
            return new CacheState(await RefreshAsync(linked, tree, source, cancellationToken), Stale: false, DocsOutcome.Ok);
        }
        catch (GitHubInstallationGoneException)
        {
            return new CacheState(null, Stale: false, DocsOutcome.InstallationGone);
        }
        catch (GitHubUnavailableException exception)
        {
            logger.LogWarning(exception, "Refreshing the docs of project {ProjectId} from GitHub failed.", linked.Link.ProjectId);
            db.ChangeTracker.Clear();
            return read
                ? new CacheState(tree, Stale: true, DocsOutcome.Ok)
                : new CacheState(null, Stale: false, DocsOutcome.Unavailable);
        }
    }

    private async Task<DocTree> RefreshAsync(LinkedRepository linked, DocTree? tree, string source, CancellationToken cancellationToken)
    {
        var link = linked.Link;
        var installationId = linked.Installation.InstallationId;
        var repository = link.RepositoryFullName;

        var head = await github.GetBranchHeadAsync(installationId, repository, link.Branch, cancellationToken);
        var sameSource = tree is not null && tree.Source == source;

        // The branch has not moved since the last read: the folder is as cached, so its SHA and tree are not asked for.
        var unchanged = head is not null && sameSource && !tree!.FolderMissing && tree.CommitSha == head.Sha;
        string? folderSha = null;
        if (head is not null && !unchanged)
        {
            folderSha = link.DocsPath.Length == 0
                ? head.TreeSha
                : await github.GetFolderShaAsync(installationId, repository, head.Sha, link.DocsPath.TrimEnd('/'), cancellationToken);
        }

        var cached = sameSource
            ? await db.DocFiles.AsNoTracking()
                .Where(f => f.TreeId == tree!.Id)
                .Select(f => new CachedFile(f.Id, f.Path, f.BlobSha, f.Size, f.Content != null))
                .ToListAsync(cancellationToken)
            : [];
        var cachedByPath = cached.ToDictionary(f => f.Path, StringComparer.Ordinal);

        // The files the folder has now: read from GitHub when the branch moved, otherwise the cached list.
        List<(string Path, string Sha, int Size)> current;
        var truncated = false;
        if (unchanged)
        {
            current = cached.Select(f => (f.Path, f.BlobSha, f.Size)).ToList();
            truncated = tree!.Truncated;
        }
        else if (folderSha is null)
        {
            current = [];
        }
        else
        {
            var gitTree = await github.GetTreeAsync(installationId, repository, folderSha, cancellationToken);
            var markdown = gitTree.Entries
                .Where(e => e.Type == "blob" && DocPath.IsMarkdown(e.Path) && DocPath.Normalize(e.Path) is not null)
                .OrderBy(e => e.Path, StringComparer.Ordinal)
                .ToList();
            truncated = gitTree.Truncated || markdown.Count > DocFile.MaxFiles;
            current = markdown.Take(DocFile.MaxFiles)
                .Select(e => (e.Path, e.Sha, (int)Math.Min(e.Size, int.MaxValue)))
                .ToList();
        }

        var removed = cached
            .Where(f => !current.Exists(c => c.Path == f.Path && c.Sha == f.BlobSha))
            .Select(f => f.Id)
            .ToList();
        var added = current
            .Where(c => !(cachedByPath.TryGetValue(c.Path, out var f) && f.BlobSha == c.Sha))
            .ToList();
        var waiting = current
            .Where(c => cachedByPath.TryGetValue(c.Path, out var f) && f.BlobSha == c.Sha && !f.HasContent && c.Size <= DocFile.MaxBytes)
            .ToList();

        // New and changed files first, then ones still waiting for their content; each within the refresh's budget.
        var toFetch = added.Concat(waiting)
            .Where(c => c.Size <= DocFile.MaxBytes)
            .Take(MaxBlobsPerRefresh)
            .ToList();
        var contents = new Dictionary<string, string>(StringComparer.Ordinal);
        await Parallel.ForEachAsync(
            toFetch,
            new ParallelOptions { MaxDegreeOfParallelism = BlobFetchConcurrency, CancellationToken = cancellationToken },
            async (file, ct) =>
            {
                var text = Decode(await github.GetBlobAsync(installationId, repository, file.Sha, ct));
                lock (contents)
                {
                    contents[file.Sha] = text;
                }
            });

        var orgId = tenant.OrgId ?? throw new InvalidOperationException("Reading docs needs a tenant.");
        var now = time.GetUtcNow();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (tree is null)
            {
                tree = new DocTree
                {
                    Id = Guid.CreateVersion7(now),
                    OrgId = orgId,
                    LinkId = link.Id,
                    ProjectId = link.ProjectId,
                };
                db.DocTrees.Add(tree);
            }
            else if (!sameSource)
            {
                var treeId = tree.Id;
                await db.DocFiles.Where(f => f.TreeId == treeId).ExecuteDeleteAsync(cancellationToken);
            }

            if (removed.Count > 0)
            {
                await db.DocFiles.Where(f => removed.Contains(f.Id)).ExecuteDeleteAsync(cancellationToken);
            }

            foreach (var file in waiting)
            {
                if (contents.TryGetValue(file.Sha, out var text))
                {
                    var id = cachedByPath[file.Path].Id;
                    var title = DocPath.TitleOf(file.Path, text);
                    await db.DocFiles
                        .Where(f => f.Id == id && f.BlobSha == file.Sha)
                        .ExecuteUpdateAsync(s => s.SetProperty(f => f.Content, text).SetProperty(f => f.Title, title), cancellationToken);
                }
            }

            foreach (var file in added)
            {
                var text = contents.GetValueOrDefault(file.Sha);
                db.DocFiles.Add(new DocFile
                {
                    Id = Guid.CreateVersion7(now),
                    OrgId = orgId,
                    TreeId = tree.Id,
                    Path = file.Path,
                    BlobSha = file.Sha,
                    Title = DocPath.TitleOf(file.Path, text),
                    Content = text,
                    Size = file.Size,
                });
            }

            tree.Source = source;
            tree.CommitSha = head?.Sha;
            tree.CommitMessage = head is null ? null : Truncate(head.Message, DocFile.TitleMaxLength);
            tree.CommitAuthor = head is null ? null : Truncate(head.Author, 200);
            tree.CommittedAt = head?.CommittedAt;
            tree.CheckedAt = now;
            tree.FolderMissing = !unchanged && folderSha is null;
            tree.Truncated = truncated;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return tree;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: GitHubDbContext.DocTreeLinkIndexName or GitHubDbContext.DocFilePathIndexName,
        })
        {
            // Another request refreshed the same folder at the same time; its result is just as current.
            db.ChangeTracker.Clear();
            return await db.DocTrees.AsNoTracking().FirstAsync(t => t.LinkId == link.Id, cancellationToken);
        }
    }

    private static DocCommit? ToCommit(ProjectRepository link, string? sha, string? message, string? author, DateTimeOffset? committedAt)
        => sha is null || committedAt is null
            ? null
            : new DocCommit(sha, message ?? string.Empty, author ?? string.Empty, committedAt.Value, GitHubUrls.Commit(link.RepositoryFullName, sha));

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;

    [GeneratedRegex(@"[\p{L}\p{N}_]+")]
    private static partial Regex Words();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private sealed record LinkedRepository(ProjectRepository Link, GitHubInstallation Installation);

    private sealed record CacheState(DocTree? Tree, bool Stale, DocsOutcome Outcome);

    private sealed record CachedFile(Guid Id, string Path, string BlobSha, int Size, bool HasContent);

    /// <summary>A row of the search query.</summary>
    private sealed class SearchRow
    {
        public string Path { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Snippet { get; init; } = string.Empty;
    }
}
