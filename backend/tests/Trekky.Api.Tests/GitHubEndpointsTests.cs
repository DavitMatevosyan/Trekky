using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Trekky.Api.Tests;

/// <summary>
/// TREK-74: an organization connects installations of the Trekky GitHub App and links each project to a repository,
/// docs folder and branch, all checked against a fake GitHub (ADR-0028).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class GitHubEndpointsTests(TrekkyApiFactory factory)
{
    private FakeGitHub GitHub => factory.GitHub;

    [Fact]
    public async Task A_new_organization_sees_where_to_install_the_app_and_no_connections()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);

        var settings = await GetAsync<GitHubSettingsBody>(owner, "/api/v1/github");

        Assert.True(settings.Configured);
        Assert.Equal(new Uri(GitHub.BaseUrl, $"apps/{FakeGitHub.AppSlug}/installations/new").ToString(), settings.InstallUrl);
        Assert.Empty(settings.Installations);
    }

    [Fact]
    public async Task An_admin_connects_an_installation_their_GitHub_user_can_access()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var installationId = GitHub.AddInstallation("acme-docs");
        var code = GitHub.CodeFor($"user-{installationId}", installationId);

        var response = await ConnectAsync(owner, code, installationId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var connection = (await response.Content.ReadFromJsonAsync<GitHubConnectionBody>())!;
        Assert.Equal(installationId, connection.InstallationId);
        Assert.Equal("acme-docs", connection.Account);
        Assert.Equal("Organization", connection.AccountType);
        Assert.Equal($"https://github.com/organizations/acme-docs/settings/installations/{installationId}", connection.ManageUrl);
        Assert.Equal("/api/v1/github", response.Headers.Location?.ToString());

        var settings = await GetAsync<GitHubSettingsBody>(owner, "/api/v1/github");
        Assert.Equal(connection.Id, Assert.Single(settings.Installations).Id);
        Assert.Contains("GitHubConnected", await OutboxEventsAsync(connection.Id));
    }

    [Fact]
    public async Task Connecting_an_installation_again_returns_the_existing_connection()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var installationId = GitHub.AddInstallation("again", "User");
        var user = $"user-{installationId}";
        var first = await (await ConnectAsync(owner, GitHub.CodeFor(user, installationId), installationId))
            .Content.ReadFromJsonAsync<GitHubConnectionBody>();

        var again = await ConnectAsync(owner, GitHub.CodeFor(user, installationId), installationId);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(first!.Id, (await again.Content.ReadFromJsonAsync<GitHubConnectionBody>())!.Id);
        Assert.Equal($"https://github.com/settings/installations/{installationId}", first.ManageUrl);
        Assert.Single((await GetAsync<GitHubSettingsBody>(owner, "/api/v1/github")).Installations);
    }

    [Fact]
    public async Task An_installation_id_the_GitHub_user_cannot_access_is_refused()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var victims = GitHub.AddInstallation("someone-else");
        var mine = GitHub.AddInstallation("mine");

        // The redirect's installation_id is forged: the code is real, but its user only has their own installation.
        var response = await ConnectAsync(owner, GitHub.CodeFor($"user-{mine}", mine), victims);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity);
        Assert.Empty((await GetAsync<GitHubSettingsBody>(owner, "/api/v1/github")).Installations);
    }

    [Fact]
    public async Task A_user_who_only_reads_some_of_the_installations_repositories_cannot_connect_it()
    {
        using var contractor = await AuthFlow.SignedUpUserAsync(factory, "Contractor org");
        var installationId = GitHub.AddInstallation(
            "acme-private", "Organization", Repository("acme-private/shared"), Repository("acme-private/secret"));

        // GitHub lists the installation for anyone who can read one of its repositories.
        var reader = await ConnectAsync(contractor, GitHub.CodeForReader($"reader-{installationId}", installationId, "acme-private/shared"), installationId);
        var readerOfAll = await ConnectAsync(
            contractor,
            GitHub.CodeForReader($"reader-all-{installationId}", installationId, "acme-private/shared", "acme-private/secret"),
            installationId);

        await AssertProblemAsync(reader, HttpStatusCode.UnprocessableEntity);
        await AssertProblemAsync(readerOfAll, HttpStatusCode.UnprocessableEntity);
        Assert.Empty((await GetAsync<GitHubSettingsBody>(contractor, "/api/v1/github")).Installations);

        // The account's real admin can still connect it to their own organization.
        using var owner = await AuthFlow.SignedUpUserAsync(factory, "Acme");
        Assert.Equal(HttpStatusCode.Created, (await ConnectAsync(owner, GitHub.CodeFor($"admin-{installationId}", installationId), installationId)).StatusCode);
    }

    [Fact]
    public async Task The_one_off_GitHub_user_token_is_revoked_after_the_check()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var installationId = GitHub.AddInstallation("revoke", "Organization", Repository("revoke/docs"));
        var refused = GitHub.AddInstallation("revoke-refused");
        var user = $"user-{installationId}";

        Assert.Equal(HttpStatusCode.Created, (await ConnectAsync(owner, GitHub.CodeFor(user, installationId), installationId)).StatusCode);
        await AssertProblemAsync(await ConnectAsync(owner, GitHub.CodeFor(user, installationId), refused), HttpStatusCode.UnprocessableEntity);

        Assert.Equal(0, GitHub.ActiveUserTokens(user));
    }

    [Fact]
    public async Task A_code_GitHub_rejects_connects_nothing()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var installationId = GitHub.AddInstallation("rejected");
        var code = GitHub.CodeFor($"user-{installationId}", installationId);
        Assert.Equal(HttpStatusCode.Created, (await ConnectAsync(owner, code, installationId)).StatusCode);
        using var other = await AuthFlow.SignedUpUserAsync(factory);

        // GitHub codes work once.
        var reused = await ConnectAsync(other, code, installationId);
        var madeUp = await ConnectAsync(other, "not-a-real-code", installationId);

        await AssertProblemAsync(reused, HttpStatusCode.UnprocessableEntity);
        await AssertProblemAsync(madeUp, HttpStatusCode.UnprocessableEntity);
        Assert.Empty((await GetAsync<GitHubSettingsBody>(other, "/api/v1/github")).Installations);
    }

    [Fact]
    public async Task An_installation_another_organization_connected_answers_409()
    {
        using var first = await AuthFlow.SignedUpUserAsync(factory, "First");
        using var second = await AuthFlow.SignedUpUserAsync(factory, "Second");
        var installationId = GitHub.AddInstallation("shared");
        var user = $"user-{installationId}";
        Assert.Equal(HttpStatusCode.Created, (await ConnectAsync(first, GitHub.CodeFor(user, installationId), installationId)).StatusCode);

        var response = await ConnectAsync(second, GitHub.CodeFor(user, installationId), installationId);

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Empty((await GetAsync<GitHubSettingsBody>(second, "/api/v1/github")).Installations);
    }

    [Fact]
    public async Task Missing_or_bad_redirect_values_are_validation_problems()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);

        var response = await SendAsync(owner, HttpMethod.Post, "/api/v1/github/installations", JsonContent.Create(new { code = "", installationId = 0 }));

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Contains("code", problem.Errors!.Keys);
        Assert.Contains("installationId", problem.Errors!.Keys);
    }

    [Fact]
    public async Task Repositories_are_listed_by_name_and_the_installation_token_is_reused()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var connection = await ConnectNewAsync(owner, Repository("acme/zeta"), Repository("acme/Alpha", isPrivate: true));

        var first = await GetAsync<GitHubRepositoryListBody>(owner, $"/api/v1/github/installations/{connection.Id}/repositories");
        var second = await GetAsync<GitHubRepositoryListBody>(owner, $"/api/v1/github/installations/{connection.Id}/repositories");

        Assert.Equal(["acme/Alpha", "acme/zeta"], first.Items.Select(r => r.FullName));
        Assert.True(first.Items[0].Private);
        Assert.False(first.Truncated);
        Assert.Equal(first.Items.Select(r => r.Id), second.Items.Select(r => r.Id));
        Assert.Equal(1, GitHub.TokenRequests(connection.InstallationId));
    }

    [Fact]
    public async Task Linking_a_project_checks_the_repository_branch_and_folder_and_uses_the_defaults()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var repository = Repository("acme/handbook");
        var connection = await ConnectNewAsync(owner, repository);
        var project = await CreateProjectAsync(owner, "DOCS");

        var response = await LinkAsync(owner, project, new { installationId = connection.Id, repository = "ACME/handbook" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        var link = (await response.Content.ReadFromJsonAsync<ProjectRepositoryBody>())!;
        Assert.Equal(project, link.ProjectId);
        Assert.Equal(connection.Id, link.InstallationId);
        Assert.Equal(repository.Id, link.RepositoryId);
        Assert.Equal("acme/handbook", link.Repository);
        Assert.Equal("docs/", link.DocsPath);
        Assert.Equal("dev", link.Branch);
        Assert.Equal("https://github.com/acme/handbook/tree/dev/docs", link.HtmlUrl);

        var read = await SendAsync(owner, HttpMethod.Get, $"/api/v1/projects/{project}/github");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(response.Headers.ETag, read.Headers.ETag);
        Assert.Contains("ProjectRepositoryLinked", await OutboxEventsAsync(project));
    }

    [Fact]
    public async Task A_branch_with_slashes_and_a_nested_folder_are_found_on_GitHub()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var repository = Repository("acme/nested") with { Branches = ["main", "release/1.0"], Folders = ["docs", "guides/en"] };
        var connection = await ConnectNewAsync(owner, repository);
        var project = await CreateProjectAsync(owner, "NEST");

        var response = await LinkAsync(owner, project, new
        {
            installationId = connection.Id, repository = "acme/nested", docsPath = "/guides/en", branch = "release/1.0",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var link = (await response.Content.ReadFromJsonAsync<ProjectRepositoryBody>())!;
        Assert.Equal("guides/en/", link.DocsPath);
        Assert.Equal("https://github.com/acme/nested/tree/release/1.0/guides/en", link.HtmlUrl);
    }

    [Fact]
    public async Task The_repository_root_can_be_the_docs_folder()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var connection = await ConnectNewAsync(owner, Repository("acme/wiki"));
        var project = await CreateProjectAsync(owner, "ROOT");

        var response = await LinkAsync(owner, project, new { installationId = connection.Id, repository = "acme/wiki", docsPath = "/" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(string.Empty, (await response.Content.ReadFromJsonAsync<ProjectRepositoryBody>())!.DocsPath);
    }

    [Theory]
    [InlineData("acme/not-installed", "dev", "docs", "repository")]
    [InlineData("acme/public-elsewhere", "dev", "docs", "repository")]
    [InlineData("acme/checked", "feature", "docs", "branch")]
    [InlineData("acme/checked", "dev", "missing", "docsPath")]
    [InlineData("acme/checked", "dev", "README.md", "docsPath")]
    public async Task Settings_GitHub_refuses_are_reported_on_their_field(string repository, string branch, string docsPath, string field)
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        GitHub.AddPublicRepository(Repository("acme/public-elsewhere"));
        var connection = await ConnectNewAsync(owner, Repository("acme/checked"));
        var project = await CreateProjectAsync(owner, "CHK");

        var response = await LinkAsync(owner, project, new { installationId = connection.Id, repository, branch, docsPath });

        var problem = await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity);
        Assert.Equal([field], problem.Errors!.Keys);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(owner, HttpMethod.Get, $"/api/v1/projects/{project}/github")).StatusCode);
    }

    [Theory]
    [InlineData("not a repo", "docs", "dev", "repository")]
    [InlineData("acme/handbook", "../secrets", "dev", "docsPath")]
    [InlineData("acme/handbook", "docs", "bad..branch", "branch")]
    public async Task Malformed_settings_are_validation_problems(string repository, string docsPath, string branch, string field)
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(owner, "BAD");

        var response = await LinkAsync(owner, project, new { installationId = Guid.CreateVersion7(), repository, docsPath, branch });

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal([field], problem.Errors!.Keys);
    }

    [Fact]
    public async Task Changing_a_link_needs_the_current_etag_and_is_recorded()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var connection = await ConnectNewAsync(owner, Repository("acme/one"), Repository("acme/two"));
        var project = await CreateProjectAsync(owner, "CHG");
        var created = await LinkAsync(owner, project, new { installationId = connection.Id, repository = "acme/one" });
        var etag = created.Headers.ETag!.ToString();
        var change = new { installationId = connection.Id, repository = "acme/two", branch = "main" };

        var withoutEtag = await LinkAsync(owner, project, change);
        var stale = await LinkAsync(owner, project, change, ifMatch: "\"1\"");
        var changed = await LinkAsync(owner, project, change, ifMatch: etag);

        await AssertProblemAsync(withoutEtag, HttpStatusCode.PreconditionRequired);
        await AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed);
        Assert.Equal(etag, stale.Headers.ETag?.ToString());
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.NotEqual(etag, changed.Headers.ETag?.ToString());
        var link = (await changed.Content.ReadFromJsonAsync<ProjectRepositoryBody>())!;
        Assert.Equal(("acme/two", "main"), (link.Repository, link.Branch));
        Assert.Contains("ProjectRepositoryChanged", await OutboxEventsAsync(project));
    }

    [Fact]
    public async Task Unlinking_removes_the_project_link()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var connection = await ConnectNewAsync(owner, Repository("acme/gone"));
        var project = await CreateProjectAsync(owner, "UNL");
        Assert.Equal(HttpStatusCode.Created, (await LinkAsync(owner, project, new { installationId = connection.Id, repository = "acme/gone" })).StatusCode);

        var unlinked = await SendAsync(owner, HttpMethod.Delete, $"/api/v1/projects/{project}/github");
        var again = await SendAsync(owner, HttpMethod.Delete, $"/api/v1/projects/{project}/github");

        Assert.Equal(HttpStatusCode.NoContent, unlinked.StatusCode);
        await AssertProblemAsync(again, HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(owner, HttpMethod.Get, $"/api/v1/projects/{project}/github")).StatusCode);
        Assert.Contains("ProjectRepositoryUnlinked", await OutboxEventsAsync(project));
    }

    [Fact]
    public async Task Disconnecting_removes_the_connection_its_project_links_and_its_cached_token()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var connection = await ConnectNewAsync(owner, Repository("acme/disconnect"));
        var project = await CreateProjectAsync(owner, "DIS");
        Assert.Equal(HttpStatusCode.Created, (await LinkAsync(owner, project, new { installationId = connection.Id, repository = "acme/disconnect" })).StatusCode);
        var tokensBefore = GitHub.TokenRequests(connection.InstallationId);

        var response = await SendAsync(owner, HttpMethod.Delete, $"/api/v1/github/installations/{connection.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await GetAsync<GitHubSettingsBody>(owner, "/api/v1/github")).Installations);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(owner, HttpMethod.Get, $"/api/v1/projects/{project}/github")).StatusCode);
        Assert.Contains("GitHubDisconnected", await OutboxEventsAsync(connection.Id));
        Assert.Contains("ProjectRepositoryUnlinked", await OutboxEventsAsync(project));

        // Connected again, the installation needs a new token: the old one was dropped from memory.
        var reconnected = await ConnectExistingAsync(owner, connection.InstallationId);
        await GetAsync<GitHubRepositoryListBody>(owner, $"/api/v1/github/installations/{reconnected.Id}/repositories");
        Assert.Equal(tokensBefore + 1, GitHub.TokenRequests(connection.InstallationId));
    }

    [Fact]
    public async Task Another_organization_can_neither_see_nor_use_the_connection()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory, "Owner org");
        using var stranger = await AuthFlow.SignedUpUserAsync(factory, "Stranger org");
        var connection = await ConnectNewAsync(owner, Repository("acme/private"));
        var project = await CreateProjectAsync(owner, "PRV");
        Assert.Equal(HttpStatusCode.Created, (await LinkAsync(owner, project, new { installationId = connection.Id, repository = "acme/private" })).StatusCode);
        var strangersProject = await CreateProjectAsync(stranger, "STR");

        Assert.Empty((await GetAsync<GitHubSettingsBody>(stranger, "/api/v1/github")).Installations);
        await AssertProblemAsync(
            await SendAsync(stranger, HttpMethod.Get, $"/api/v1/github/installations/{connection.Id}/repositories"), HttpStatusCode.NotFound);
        await AssertProblemAsync(
            await SendAsync(stranger, HttpMethod.Delete, $"/api/v1/github/installations/{connection.Id}"), HttpStatusCode.NotFound);
        await AssertProblemAsync(await SendAsync(stranger, HttpMethod.Get, $"/api/v1/projects/{project}/github"), HttpStatusCode.NotFound);
        await AssertProblemAsync(
            await LinkAsync(stranger, project, new { installationId = connection.Id, repository = "acme/private" }), HttpStatusCode.NotFound);
        var borrowed = await LinkAsync(stranger, strangersProject, new { installationId = connection.Id, repository = "acme/private" });
        Assert.Equal(["installationId"], (await AssertProblemAsync(borrowed, HttpStatusCode.UnprocessableEntity)).Errors!.Keys);
    }

    [Fact]
    public async Task Members_and_viewers_read_the_link_but_cannot_manage_GitHub()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var connection = await ConnectNewAsync(owner, Repository("acme/roles"));
        var project = await CreateProjectAsync(owner, "ROL");
        Assert.Equal(HttpStatusCode.Created, (await LinkAsync(owner, project, new { installationId = connection.Id, repository = "acme/roles" })).StatusCode);

        foreach (var role in new[] { "member", "viewer" })
        {
            using var user = await JoinAsync(owner, role);
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(user, HttpMethod.Get, $"/api/v1/projects/{project}/github")).StatusCode);

            HttpResponseMessage[] refused =
            [
                await SendAsync(user, HttpMethod.Get, "/api/v1/github"),
                await ConnectAsync(user, "code", 1),
                await SendAsync(user, HttpMethod.Get, $"/api/v1/github/installations/{connection.Id}/repositories"),
                await SendAsync(user, HttpMethod.Delete, $"/api/v1/github/installations/{connection.Id}"),
                await LinkAsync(user, project, new { installationId = connection.Id, repository = "acme/roles" }, ifMatch: "*"),
                await SendAsync(user, HttpMethod.Delete, $"/api/v1/projects/{project}/github"),
            ];
            foreach (var response in refused)
            {
                await AssertProblemAsync(response, HttpStatusCode.Forbidden);
            }
        }

        Assert.Single((await GetAsync<GitHubSettingsBody>(owner, "/api/v1/github")).Installations);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(owner, HttpMethod.Get, $"/api/v1/projects/{project}/github")).StatusCode);
    }

    [Fact]
    public async Task An_installation_removed_on_GitHub_answers_409_and_a_GitHub_error_502()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var uninstalled = await ConnectNewAsync(owner, Repository("acme/uninstalled"));
        var failing = await ConnectNewAsync(owner, Repository("acme/failing"));
        GitHub.Uninstall(uninstalled.InstallationId);
        GitHub.Fail(failing.InstallationId);

        await AssertProblemAsync(
            await SendAsync(owner, HttpMethod.Get, $"/api/v1/github/installations/{uninstalled.Id}/repositories"), HttpStatusCode.Conflict);
        await AssertProblemAsync(
            await SendAsync(owner, HttpMethod.Get, $"/api/v1/github/installations/{failing.Id}/repositories"), HttpStatusCode.BadGateway);

        // Disconnecting still works, so the organization can clean up.
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(owner, HttpMethod.Delete, $"/api/v1/github/installations/{uninstalled.Id}")).StatusCode);
    }

    [Fact]
    public async Task No_GitHub_token_or_key_ever_appears_in_a_response_or_the_database()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var connection = await ConnectNewAsync(owner, Repository("acme/secret"));
        var project = await CreateProjectAsync(owner, "SEC");
        var bodies = new List<string>
        {
            await (await LinkAsync(owner, project, new { installationId = connection.Id, repository = "acme/secret" })).Content.ReadAsStringAsync(),
            await (await SendAsync(owner, HttpMethod.Get, "/api/v1/github")).Content.ReadAsStringAsync(),
            await (await SendAsync(owner, HttpMethod.Get, $"/api/v1/github/installations/{connection.Id}/repositories")).Content.ReadAsStringAsync(),
            await (await SendAsync(owner, HttpMethod.Get, $"/api/v1/projects/{project}/github")).Content.ReadAsStringAsync(),
        };
        bodies.Add(await DumpGitHubSchemaAsync());

        var secrets = GitHub.IssuedTokens.Append(FakeGitHub.ClientSecret).Append("PRIVATE KEY").ToList();
        Assert.NotEmpty(GitHub.IssuedTokens);
        foreach (var body in bodies)
        {
            Assert.DoesNotContain(secrets, secret => body.Contains(secret, StringComparison.Ordinal));
        }
    }

    private static FakeRepository Repository(string fullName, bool isPrivate = false)
        => new(Random.Shared.NextInt64(1, long.MaxValue), fullName, isPrivate, ["dev", "main"], ["docs"], ["README.md"]);

    /// <summary>A new installation reaching <paramref name="repositories"/>, connected to <paramref name="user"/>'s organization.</summary>
    private async Task<GitHubConnectionBody> ConnectNewAsync(SignedUpUser user, params FakeRepository[] repositories)
    {
        var installationId = GitHub.AddInstallation($"acct-{GitHub.NextId()}", "Organization", repositories);
        return await ConnectExistingAsync(user, installationId);
    }

    private async Task<GitHubConnectionBody> ConnectExistingAsync(SignedUpUser user, long installationId)
    {
        var response = await ConnectAsync(user, GitHub.CodeFor($"user-{installationId}", installationId), installationId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GitHubConnectionBody>())!;
    }

    private static Task<HttpResponseMessage> ConnectAsync(SignedUpUser user, string code, long installationId)
        => SendAsync(user, HttpMethod.Post, "/api/v1/github/installations", JsonContent.Create(new { code, installationId }));

    private static Task<HttpResponseMessage> LinkAsync(SignedUpUser user, Guid projectId, object body, string? ifMatch = null)
        => SendAsync(user, HttpMethod.Put, $"/api/v1/projects/{projectId}/github", JsonContent.Create(body), ifMatch);

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

    /// <summary>Event types queued in the github outbox about <paramref name="entityId"/> (dispatch may not have run yet).</summary>
    private async Task<List<string>> OutboxEventsAsync(Guid entityId)
    {
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT event_type FROM github.outbox WHERE envelope -> 'entity' ->> 'id' = @id ORDER BY id", connection);
        command.Parameters.AddWithValue("id", entityId.ToString());
        var types = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            types.Add(reader.GetString(0));
        }

        return types;
    }

    /// <summary>Every row of the github schema's tables as text, outbox included.</summary>
    private async Task<string> DumpGitHubSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        var dump = new System.Text.StringBuilder();
        foreach (var table in new[] { "installations", "project_repositories", "outbox" })
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
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {path} answered {response.StatusCode}");
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

    private sealed record GitHubSettingsBody(bool Configured, string? InstallUrl, List<GitHubConnectionBody> Installations);

    private sealed record GitHubConnectionBody(
        Guid Id, long InstallationId, string Account, string AccountType, string ManageUrl, DateTimeOffset ConnectedAt);

    private sealed record GitHubRepositoryListBody(List<GitHubRepositoryBody> Items, bool Truncated);

    private sealed record GitHubRepositoryBody(long Id, string FullName, string DefaultBranch, bool Private, string HtmlUrl);

    private sealed record ProjectRepositoryBody(
        Guid ProjectId, Guid InstallationId, string Account, long RepositoryId, string Repository, string DocsPath, string Branch,
        string HtmlUrl, DateTimeOffset LinkedAt);
}
