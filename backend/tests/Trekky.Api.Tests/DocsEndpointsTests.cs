using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Trekky.Api.Tests;

/// <summary>
/// TREK-75: a project's docs are read from its linked GitHub repository, cached per organization by blob SHA, served
/// with their last commit and images, and searched with Postgres full-text search (ADR-0030).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DocsEndpointsTests(TrekkyApiFactory factory)
{
    private static readonly Dictionary<string, string?> Handbook = new()
    {
        ["docs/README.md"] = "# Handbook\n\nStart with [setup](guide/setup.md).\n",
        ["docs/guide/setup.md"] = "# Setting up\n\nRun Docker Compose to start Postgres and Mailpit.\n\n![diagram](../images/flow.png)\n",
        ["docs/guide/deploy.markdown"] = "Deploying behind Tailscale Funnel.\n",
        ["docs/images/notes.txt"] = "not a doc",
        ["src/Program.cs"] = "// code, outside the docs folder",
        ["README.md"] = "# Repository readme, outside the docs folder",
    };

    private FakeGitHub GitHub => factory.GitHub;

    [Fact]
    public async Task The_tree_lists_the_Markdown_files_of_the_docs_folder_with_titles_and_the_last_commit()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var docs = await LinkedDocsAsync(owner, Handbook);

        var tree = await GetAsync<DocsTreeBody>(owner, $"/api/v1/projects/{docs.ProjectId}/docs");

        Assert.Equal(["README.md", "guide/deploy.markdown", "guide/setup.md"], tree.Files.Select(f => f.Path));
        Assert.Equal(["Handbook", "deploy", "Setting up"], tree.Files.Select(f => f.Title));
        Assert.Equal(docs.Repository, tree.Repository);
        Assert.Equal("dev", tree.Branch);
        Assert.Equal("docs/", tree.DocsPath);
        Assert.Equal($"https://github.com/{docs.Repository}/tree/dev/docs", tree.HtmlUrl);
        Assert.Equal(docs.CommitSha, tree.LastCommit!.Sha);
        Assert.Equal("Initial docs", tree.LastCommit.Message);
        Assert.Equal("Ada Lovelace", tree.LastCommit.Author);
        Assert.Equal($"https://github.com/{docs.Repository}/commit/{docs.CommitSha}", tree.LastCommit.HtmlUrl);
        Assert.False(tree.Stale);
        Assert.False(tree.FolderMissing);
        Assert.False(tree.Truncated);
    }

    [Fact]
    public async Task A_fresh_cache_is_served_without_asking_GitHub_and_refresh_asks_again()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var docs = await LinkedDocsAsync(owner, Handbook);
        var path = $"/api/v1/projects/{docs.ProjectId}/docs";

        await GetAsync<DocsTreeBody>(owner, path);
        var treesAfterFirstRead = GitHub.TreeRequests(docs.Repository);
        await GetAsync<DocsTreeBody>(owner, path);

        Assert.Equal(treesAfterFirstRead, GitHub.TreeRequests(docs.Repository));

        // A refresh at the same commit asks for the branch head only: the folder is unchanged, so its tree is not read.
        var refreshed = await SendAsync(owner, HttpMethod.Get, $"{path}?refresh=true");
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(treesAfterFirstRead, GitHub.TreeRequests(docs.Repository));
    }

    [Fact]
    public async Task A_refresh_reads_only_changed_files_and_drops_removed_ones()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var docs = await LinkedDocsAsync(owner, Handbook);
        await GetAsync<DocsTreeBody>(owner, $"/api/v1/projects/{docs.ProjectId}/docs");
        // Blob SHAs come from content, which other tests share: count this test's downloads only.
        var readmeSha = FakeGitHub.BlobSha(Encoding.UTF8.GetBytes(Handbook["docs/README.md"]!));
        var readmeDownloads = GitHub.BlobRequests(readmeSha);

        var second = GitHub.CommitFiles(docs.Repository, "dev", "Rewrite setup\n\nLonger body.", new Dictionary<string, string?>
        {
            ["docs/guide/setup.md"] = "# Setting up, again\n\nNow with Kubernetes.\n",
            ["docs/guide/deploy.markdown"] = null,
        });
        var tree = await RefreshAsync(owner, docs.ProjectId);

        Assert.Equal(["README.md", "guide/setup.md"], tree.Files.Select(f => f.Path));
        Assert.Equal("Setting up, again", tree.Files[1].Title);
        Assert.Equal(second, tree.LastCommit!.Sha);
        Assert.Equal("Rewrite setup", tree.LastCommit.Message);
        Assert.Equal(readmeDownloads, GitHub.BlobRequests(readmeSha));

        var doc = await GetAsync<DocContentBody>(owner, DocPath(docs.ProjectId, "guide/setup.md"));
        Assert.Contains("Kubernetes", doc.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_doc_is_served_with_its_own_last_commit_and_its_blob_sha_as_etag()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var docs = await LinkedDocsAsync(owner, Handbook);
        var later = GitHub.CommitFiles(docs.Repository, "dev", "Touch the readme", new Dictionary<string, string?>
        {
            ["docs/README.md"] = "# Handbook\n\nUpdated.\n",
        }, author: "Grace Hopper");

        var response = await SendAsync(owner, HttpMethod.Get, DocPath(docs.ProjectId, "guide/setup.md"));
        var setup = (await response.Content.ReadFromJsonAsync<DocContentBody>())!;
        var readme = await GetAsync<DocContentBody>(owner, DocPath(docs.ProjectId, "README.md"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setupSha = FakeGitHub.BlobSha(Encoding.UTF8.GetBytes(Handbook["docs/guide/setup.md"]!));
        Assert.Equal($"\"{setupSha}\"", response.Headers.ETag?.ToString());
        Assert.Equal("Setting up", setup.Title);
        Assert.Equal(Handbook["docs/guide/setup.md"], setup.Content);
        Assert.Equal(setupSha, setup.BlobSha);
        Assert.Equal(docs.CommitSha, setup.LastCommit!.Sha);
        Assert.Equal($"https://github.com/{docs.Repository}/blob/dev/docs/guide/setup.md", setup.HtmlUrl);
        Assert.False(setup.TooLarge);
        Assert.Equal(later, readme.LastCommit!.Sha);
        Assert.Equal("Grace Hopper", readme.LastCommit.Author);
    }

    [Theory]
    [InlineData("../README.md")]
    [InlineData("/README.md")]
    [InlineData("guide/../../src/Program.cs")]
    [InlineData("guide//setup.md")]
    [InlineData("images/notes.txt")]
    [InlineData("")]
    public async Task Paths_outside_the_docs_folder_or_not_Markdown_are_refused(string path)
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var docs = await LinkedDocsAsync(owner, Handbook);

        await AssertProblemAsync(await SendAsync(owner, HttpMethod.Get, DocPath(docs.ProjectId, path)), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_doc_that_is_not_in_the_folder_is_404()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var docs = await LinkedDocsAsync(owner, Handbook);

        await AssertProblemAsync(await SendAsync(owner, HttpMethod.Get, DocPath(docs.ProjectId, "nope.md")), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Images_in_the_docs_folder_are_served_with_their_type_and_no_others()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
        var docs = await LinkedDocsAsync(owner, Handbook, images: new Dictionary<string, byte[]?>
        {
            ["docs/images/flow.png"] = png,
            ["docs/images/flow.svg"] = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"),
            ["logo.png"] = png,
        });

        var image = await SendAsync(owner, HttpMethod.Get, ImagePath(docs.ProjectId, "images/flow.png"));
        var svg = await SendAsync(owner, HttpMethod.Get, ImagePath(docs.ProjectId, "images/flow.svg"));

        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(png, await image.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", image.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("private", image.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.Equal("image/svg+xml", svg.Content.Headers.ContentType?.MediaType);
        Assert.Contains("sandbox", svg.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);

        await AssertProblemAsync(await SendAsync(owner, HttpMethod.Get, ImagePath(docs.ProjectId, "../logo.png")), HttpStatusCode.BadRequest);
        await AssertProblemAsync(await SendAsync(owner, HttpMethod.Get, ImagePath(docs.ProjectId, "README.md")), HttpStatusCode.BadRequest);
        await AssertProblemAsync(await SendAsync(owner, HttpMethod.Get, ImagePath(docs.ProjectId, "images/missing.png")), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Search_matches_word_prefixes_and_marks_them_in_the_snippet()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var docs = await LinkedDocsAsync(owner, Handbook);

        var results = await GetAsync<DocSearchResultsBody>(owner, SearchPath(docs.ProjectId, "dock comp"));
        var none = await GetAsync<DocSearchResultsBody>(owner, SearchPath(docs.ProjectId, "kubernetes"));
        var byTitle = await GetAsync<DocSearchResultsBody>(owner, SearchPath(docs.ProjectId, "handbook"));

        var hit = Assert.Single(results.Results);
        Assert.Equal("guide/setup.md", hit.Path);
        Assert.Equal("Setting up", hit.Title);
        Assert.Equal(["Docker", "Compose"], hit.Snippet.Where(p => p.Match).Select(p => p.Text));
        Assert.Contains("Run Docker Compose to start Postgres", string.Concat(hit.Snippet.Select(p => p.Text)), StringComparison.Ordinal);
        Assert.False(results.Incomplete);
        Assert.Empty(none.Results);
        Assert.Equal("README.md", byTitle.Results[0].Path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_search_is_a_validation_problem(string query)
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var docs = await LinkedDocsAsync(owner, Handbook);

        var problem = await AssertProblemAsync(await SendAsync(owner, HttpMethod.Get, SearchPath(docs.ProjectId, query)), HttpStatusCode.BadRequest);

        Assert.Equal(["q"], problem.Errors!.Keys);
    }

    [Fact]
    public async Task Search_syntax_in_the_query_is_read_as_plain_words()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var docs = await LinkedDocsAsync(owner, Handbook);

        var results = await GetAsync<DocSearchResultsBody>(owner, SearchPath(docs.ProjectId, "postgres:* & !(') | mail"));

        Assert.Equal("guide/setup.md", Assert.Single(results.Results).Path);
    }

    [Fact]
    public async Task When_GitHub_is_down_the_cached_copy_is_served_as_stale_and_without_a_cache_it_is_502()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var cached = await LinkedDocsAsync(owner, Handbook);
        await GetAsync<DocContentBody>(owner, DocPath(cached.ProjectId, "README.md"));
        var uncached = await LinkedDocsAsync(owner, Handbook, key: "UNC");

        GitHub.FailContents(cached.Repository);
        GitHub.FailContents(uncached.Repository);
        try
        {
            var tree = await RefreshAsync(owner, cached.ProjectId);
            var doc = await GetAsync<DocContentBody>(owner, DocPath(cached.ProjectId, "README.md"));

            Assert.True(tree.Stale);
            Assert.Equal(3, tree.Files.Count);
            Assert.Equal("Handbook", doc.Title);
            await AssertProblemAsync(await SendAsync(owner, HttpMethod.Get, $"/api/v1/projects/{uncached.ProjectId}/docs"), HttpStatusCode.BadGateway);
        }
        finally
        {
            GitHub.FailContents(cached.Repository, failing: false);
            GitHub.FailContents(uncached.Repository, failing: false);
        }
    }

    [Fact]
    public async Task A_doc_over_1_MB_is_listed_but_not_downloaded_shown_or_searched()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var huge = "# Huge\n\n" + new string('x', 1024 * 1024) + " needle";
        var docs = await LinkedDocsAsync(owner, new Dictionary<string, string?>(Handbook) { ["docs/huge.md"] = huge });
        var hugeSha = FakeGitHub.BlobSha(Encoding.UTF8.GetBytes(huge));

        var tree = await GetAsync<DocsTreeBody>(owner, $"/api/v1/projects/{docs.ProjectId}/docs");
        var doc = await GetAsync<DocContentBody>(owner, DocPath(docs.ProjectId, "huge.md"));
        var search = await GetAsync<DocSearchResultsBody>(owner, SearchPath(docs.ProjectId, "needle"));

        var listed = tree.Files.Single(f => f.Path == "huge.md");
        Assert.True(listed.TooLarge);
        Assert.Equal("huge", listed.Title);
        Assert.True(doc.TooLarge);
        Assert.Null(doc.Content);
        Assert.Equal($"https://github.com/{docs.Repository}/blob/dev/docs/huge.md", doc.HtmlUrl);
        Assert.Empty(search.Results);
        Assert.False(search.Incomplete);
        Assert.Equal(0, GitHub.BlobRequests(hugeSha));
    }

    [Fact]
    public async Task A_doc_of_many_distinct_words_is_cached_and_its_start_is_searchable()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);

        // About 900 KB of distinct words, like an image pasted as base64: its full tsvector would pass Postgres's 1 MB limit.
        var words = string.Join(' ', Enumerable.Range(0, 110_000).Select(i => $"w{i:x6}"));
        var docs = await LinkedDocsAsync(owner, new Dictionary<string, string?>(Handbook) { ["docs/pasted.md"] = $"# Pasted\n\nneedle {words}" });

        var tree = await GetAsync<DocsTreeBody>(owner, $"/api/v1/projects/{docs.ProjectId}/docs");
        var search = await GetAsync<DocSearchResultsBody>(owner, SearchPath(docs.ProjectId, "needle"));

        Assert.False(tree.Files.Single(f => f.Path == "pasted.md").TooLarge);
        Assert.Equal("pasted.md", Assert.Single(search.Results).Path);
        Assert.Contains(words[..100], (await GetAsync<DocContentBody>(owner, DocPath(docs.ProjectId, "pasted.md"))).Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refreshes_racing_on_an_empty_cache_both_answer_with_the_folder()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var docs = await LinkedDocsAsync(owner, Handbook);

        var trees = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RefreshAsync(owner, docs.ProjectId)));

        Assert.All(trees, tree => Assert.Equal(["README.md", "guide/deploy.markdown", "guide/setup.md"], tree.Files.Select(f => f.Path)));
        Assert.Equal(4, await CachedRowsAsync(docs.ProjectId));
    }

    [Fact]
    public async Task A_docs_folder_deleted_on_GitHub_shows_as_missing()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var docs = await LinkedDocsAsync(owner, Handbook);
        await GetAsync<DocsTreeBody>(owner, $"/api/v1/projects/{docs.ProjectId}/docs");

        GitHub.CommitFiles(docs.Repository, "dev", "Move docs away", Handbook.Keys
            .Where(p => p.StartsWith("docs/", StringComparison.Ordinal))
            .ToDictionary(p => p, _ => (string?)null));
        var tree = await RefreshAsync(owner, docs.ProjectId);

        Assert.True(tree.FolderMissing);
        Assert.Empty(tree.Files);
        Assert.Empty((await GetAsync<DocSearchResultsBody>(owner, SearchPath(docs.ProjectId, "handbook"))).Results);
    }

    [Fact]
    public async Task Changing_the_link_shows_the_new_folder_and_unlinking_drops_the_cache()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var docs = await LinkedDocsAsync(owner, Handbook);
        await GetAsync<DocsTreeBody>(owner, $"/api/v1/projects/{docs.ProjectId}/docs");

        var link = await SendAsync(owner, HttpMethod.Get, $"/api/v1/projects/{docs.ProjectId}/github");
        var etag = link.Headers.ETag!.ToString();
        var relinked = await SendAsync(owner, HttpMethod.Put, $"/api/v1/projects/{docs.ProjectId}/github",
            JsonContent.Create(new { installationId = docs.ConnectionId, repository = docs.Repository, docsPath = "docs/guide" }), etag);
        Assert.Equal(HttpStatusCode.OK, relinked.StatusCode);

        var tree = await GetAsync<DocsTreeBody>(owner, $"/api/v1/projects/{docs.ProjectId}/docs");
        Assert.Equal(["deploy.markdown", "setup.md"], tree.Files.Select(f => f.Path));

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(owner, HttpMethod.Delete, $"/api/v1/projects/{docs.ProjectId}/github")).StatusCode);
        Assert.Equal(0, await CachedRowsAsync(docs.ProjectId));
        await AssertProblemAsync(await SendAsync(owner, HttpMethod.Get, $"/api/v1/projects/{docs.ProjectId}/docs"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Viewers_read_and_refresh_docs_and_another_organization_sees_none()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory, "Docs owner org");
        using var stranger = await AuthFlow.SignedUpUserAsync(factory, "Docs stranger org");
        var docs = await LinkedDocsAsync(owner, Handbook);
        await GetAsync<DocsTreeBody>(owner, $"/api/v1/projects/{docs.ProjectId}/docs");
        using var viewer = await JoinAsync(owner, "viewer");

        Assert.Equal(3, (await GetAsync<DocsTreeBody>(viewer, $"/api/v1/projects/{docs.ProjectId}/docs")).Files.Count);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(viewer, HttpMethod.Get, $"/api/v1/projects/{docs.ProjectId}/docs?refresh=true")).StatusCode);
        Assert.Equal("Handbook", (await GetAsync<DocContentBody>(viewer, DocPath(docs.ProjectId, "README.md"))).Title);

        foreach (var path in new[]
        {
            $"/api/v1/projects/{docs.ProjectId}/docs",
            DocPath(docs.ProjectId, "README.md"),
            SearchPath(docs.ProjectId, "handbook"),
            ImagePath(docs.ProjectId, "images/flow.png"),
        })
        {
            await AssertProblemAsync(await SendAsync(stranger, HttpMethod.Get, path), HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task A_project_without_a_linked_repository_has_no_docs()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(owner, "NOD");

        var problem = await AssertProblemAsync(await SendAsync(owner, HttpMethod.Get, $"/api/v1/projects/{project}/docs"), HttpStatusCode.NotFound);

        Assert.Equal("No GitHub repository linked", problem.Title);
    }

    [Fact]
    public async Task No_GitHub_token_appears_in_docs_responses_or_the_cache()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var docs = await LinkedDocsAsync(owner, Handbook);
        var bodies = new List<string>
        {
            await (await SendAsync(owner, HttpMethod.Get, $"/api/v1/projects/{docs.ProjectId}/docs")).Content.ReadAsStringAsync(),
            await (await SendAsync(owner, HttpMethod.Get, DocPath(docs.ProjectId, "README.md"))).Content.ReadAsStringAsync(),
            await (await SendAsync(owner, HttpMethod.Get, SearchPath(docs.ProjectId, "handbook"))).Content.ReadAsStringAsync(),
            await DumpDocsCacheAsync(),
        };

        Assert.NotEmpty(GitHub.IssuedTokens);
        foreach (var body in bodies)
        {
            Assert.DoesNotContain(GitHub.IssuedTokens, token => body.Contains(token, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// A repository with <paramref name="files"/> committed on <c>dev</c>, reached by a new installation connected to
    /// <paramref name="owner"/>'s organization and linked to a new project with docs folder <c>docs/</c>.
    /// </summary>
    private async Task<LinkedDocs> LinkedDocsAsync(
        SignedUpUser owner, IReadOnlyDictionary<string, string?> files, IReadOnlyDictionary<string, byte[]?>? images = null, string key = "DOC")
    {
        var repository = $"acme/handbook-{GitHub.NextId()}";
        var commit = GitHub.CommitFiles(repository, "dev", "Initial docs", files);
        if (images is not null)
        {
            commit = GitHub.CommitBytes(repository, "dev", "Initial docs", images);
        }

        var installationId = GitHub.AddInstallation(
            $"acct-{GitHub.NextId()}", "Organization", new FakeRepository(GitHub.NextId(), repository, false, ["dev"], [], []));
        var connect = await SendAsync(owner, HttpMethod.Post, "/api/v1/github/installations",
            JsonContent.Create(new { code = GitHub.CodeFor($"user-{installationId}", installationId), installationId }));
        Assert.Equal(HttpStatusCode.Created, connect.StatusCode);
        var connectionId = (await connect.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var project = await CreateProjectAsync(owner, key);
        var link = await SendAsync(owner, HttpMethod.Put, $"/api/v1/projects/{project}/github",
            JsonContent.Create(new { installationId = connectionId, repository }));
        Assert.Equal(HttpStatusCode.Created, link.StatusCode);
        return new LinkedDocs(project, connectionId, repository, commit);
    }

    private static string DocPath(Guid projectId, string path) => $"/api/v1/projects/{projectId}/docs/file?path={Uri.EscapeDataString(path)}";

    private static string ImagePath(Guid projectId, string path) => $"/api/v1/projects/{projectId}/docs/image?path={Uri.EscapeDataString(path)}";

    private static string SearchPath(Guid projectId, string query) => $"/api/v1/projects/{projectId}/docs/search?q={Uri.EscapeDataString(query)}";

    private static async Task<DocsTreeBody> RefreshAsync(SignedUpUser user, Guid projectId)
    {
        var response = await SendAsync(user, HttpMethod.Get, $"/api/v1/projects/{projectId}/docs?refresh=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DocsTreeBody>())!;
    }

    private static async Task<Guid> CreateProjectAsync(SignedUpUser user, string key)
    {
        var response = await SendAsync(user, HttpMethod.Post, "/api/v1/projects", JsonContent.Create(new { name = $"Project {key}", key }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>A new user moved into <paramref name="owner"/>'s organization with <paramref name="role"/>, with fresh tokens.</summary>
    private async Task<SignedUpUser> JoinAsync(SignedUpUser owner, string role)
    {
        var user = await AuthFlow.SignedUpUserAsync(factory, "Left behind");
        var userId = (await GetAsync<MeBody>(user, "/api/v1/me")).User.Id;
        var orgId = (await GetAsync<MeBody>(owner, "/api/v1/me")).Organization.Id;
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE identity.memberships SET org_id = @org, role = @role WHERE user_id = @user", connection);
        command.Parameters.AddWithValue("org", orgId);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("user", userId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
        return user with { Tokens = await AuthFlow.GetTokensAsync(user.Client) };
    }

    /// <summary>Cached trees and files of the project, counted past row-level security.</summary>
    private async Task<long> CachedRowsAsync(Guid projectId)
    {
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT (SELECT count(*) FROM github.doc_trees WHERE project_id = @project)
                 + (SELECT count(*) FROM github.doc_files f JOIN github.doc_trees t ON t.id = f.tree_id WHERE t.project_id = @project)
            """,
            connection);
        command.Parameters.AddWithValue("project", projectId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<string> DumpDocsCacheAsync()
    {
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        var dump = new StringBuilder();
        foreach (var table in new[] { "doc_trees", "doc_files" })
        {
            await using var command = new NpgsqlCommand($"SELECT t::text FROM github.{table} t", connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                dump.AppendLine(reader.GetString(0));
            }
        }

        return dump.ToString();
    }

    private static async Task<T> GetAsync<T>(SignedUpUser user, string path)
    {
        var response = await SendAsync(user, HttpMethod.Get, path);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {path} answered {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static Task<HttpResponseMessage> SendAsync(
        SignedUpUser user, HttpMethod method, string path, HttpContent? content = null, string? ifMatch = null)
    {
        var request = AuthFlow.WithBearer(method, path, user.Tokens.AccessToken);
        request.Content = content;
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return user.Client.SendAsync(request);
    }

    private static async Task<ProblemBody> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.True(
            response.StatusCode == status,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} answered {response.StatusCode}, expected {status}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return (await response.Content.ReadFromJsonAsync<ProblemBody>())!;
    }

    private sealed record LinkedDocs(Guid ProjectId, Guid ConnectionId, string Repository, string CommitSha);

    private sealed record DocCommitBody(string Sha, string Message, string Author, DateTimeOffset CommittedAt, string HtmlUrl);

    private sealed record DocListingBody(string Path, string Title, int Size, bool TooLarge);

    private sealed record DocsTreeBody(
        Guid ProjectId, string Repository, string Branch, string DocsPath, string HtmlUrl, DocCommitBody? LastCommit,
        DateTimeOffset? CheckedAt, bool Stale, bool FolderMissing, bool Truncated, List<DocListingBody> Files);

    private sealed record DocContentBody(
        string Path, string Title, string? Content, string BlobSha, int Size, bool TooLarge, DocCommitBody? LastCommit, string HtmlUrl, bool Stale);

    private sealed record DocSnippetPartBody(string Text, bool Match);

    private sealed record DocSearchHitBody(string Path, string Title, List<DocSnippetPartBody> Snippet);

    private sealed record DocSearchResultsBody(string Query, List<DocSearchHitBody> Results, bool Incomplete);
}
