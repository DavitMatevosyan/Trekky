using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Trekky.Api.Tests;

/// <summary>
/// TREK-46: Owner, Admin, Member and Viewer are checked by the API on every endpoint (ADR-0025). Viewers only read;
/// Members create and edit work items and comments; Admins and Owners also create projects.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RoleAuthorizationTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task A_viewer_reads_everything_in_the_organization()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(owner, "VIEW");
        var item = await CreateItemAsync(owner, project, "Read me");
        Assert.Equal(HttpStatusCode.Created, (await AddCommentAsync(owner, item.Id, "Hello")).StatusCode);
        using var viewer = await JoinAsync(owner, "viewer");

        string[] paths =
        [
            "/api/v1/me",
            "/api/v1/members",
            "/api/v1/projects",
            $"/api/v1/projects/{project}",
            "/api/v1/projects/by-key/VIEW",
            $"/api/v1/projects/{project}/work-items",
            $"/api/v1/projects/{project}/board",
            $"/api/v1/work-items/{item.Id}",
            "/api/v1/work-items/by-key/VIEW-1",
            $"/api/v1/work-items/{item.Id}/comments",
            $"/api/v1/work-items/{item.Id}/history",
        ];
        foreach (var path in paths)
        {
            using var request = AuthFlow.WithBearer(HttpMethod.Get, path, viewer.Tokens.AccessToken);
            var response = await viewer.Client.SendAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {path} answered {response.StatusCode}");
        }

        var me = await GetAsync<MeBody>(viewer, "/api/v1/me");
        Assert.Equal("viewer", me.Role);
        Assert.Equal(await OrgIdAsync(owner), me.Organization.Id);
    }

    [Fact]
    public async Task A_viewer_gets_403_for_every_write_and_nothing_changes()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(owner, "RO");
        var item = await CreateItemAsync(owner, project, "Untouched");
        var comment = await AddCommentAsync(owner, item.Id, "Mine");
        var commentBody = await comment.Content.ReadFromJsonAsync<JsonElement>();
        var commentId = commentBody.GetProperty("id").GetGuid();
        var commentEtag = commentBody.GetProperty("etag").GetString()!;
        using var viewer = await JoinAsync(owner, "viewer");

        await AssertForbiddenAsync(await CreateProjectRawAsync(viewer, "NOPE"));
        await AssertForbiddenAsync(await CreateItemRawAsync(viewer, project, "Nope"));
        await AssertForbiddenAsync(await PatchItemAsync(viewer, item.Id, item.ETag, """{ "title": "Changed" }"""));
        await AssertForbiddenAsync(await ChangeStatusAsync(viewer, item.Id, item.ETag, "in_progress"));
        await AssertForbiddenAsync(await MoveAsync(viewer, item.Id, item.ETag, "in_progress"));
        await AssertForbiddenAsync(await AddCommentAsync(viewer, item.Id, "Nope"));
        await AssertForbiddenAsync(await EditCommentAsync(viewer, commentId, commentEtag, "Nope"));
        await AssertForbiddenAsync(await SendAsync(viewer, HttpMethod.Delete, $"/api/v1/comments/{commentId}"));

        var after = await GetAsync<JsonElement>(owner, $"/api/v1/work-items/{item.Id}");
        Assert.Equal("Untouched", after.GetProperty("title").GetString());
        Assert.Equal("todo", after.GetProperty("status").GetString());
        var projects = await GetAsync<JsonElement>(owner, "/api/v1/projects");
        Assert.Single(projects.GetProperty("items").EnumerateArray());
        var items = await GetAsync<JsonElement>(owner, $"/api/v1/projects/{project}/work-items");
        Assert.Single(items.GetProperty("items").EnumerateArray());
        var comments = await GetAsync<JsonElement>(owner, $"/api/v1/work-items/{item.Id}/comments");
        Assert.Equal(["Mine"], comments.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("bodyMd").GetString()));
    }

    [Fact]
    public async Task A_member_turned_viewer_can_no_longer_change_their_own_comment()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(owner, "OWN");
        using var member = await JoinAsync(owner, "member");
        var item = await CreateItemAsync(member, project, "Commented");
        var comment = await (await AddCommentAsync(member, item.Id, "Mine")).Content.ReadFromJsonAsync<JsonElement>();
        var commentId = comment.GetProperty("id").GetGuid();

        // The author check would let them through; only the role stops them.
        await SetRoleAsync(member, "viewer");
        // The same user and HTTP client, with tokens that carry the new role.
        var viewer = await RefreshedAsync(member);

        await AssertForbiddenAsync(await EditCommentAsync(viewer, commentId, comment.GetProperty("etag").GetString()!, "Changed"));
        await AssertForbiddenAsync(await SendAsync(viewer, HttpMethod.Delete, $"/api/v1/comments/{commentId}"));
        var after = await GetAsync<JsonElement>(owner, $"/api/v1/comments/{commentId}");
        Assert.Equal("Mine", after.GetProperty("bodyMd").GetString());
    }

    [Fact]
    public async Task A_member_creates_and_edits_work_items_and_comments()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(owner, "MEM");
        using var member = await JoinAsync(owner, "member");

        var item = await CreateItemAsync(member, project, "By a member");
        var patched = await PatchItemAsync(member, item.Id, item.ETag, """{ "title": "Renamed by a member" }""");
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        var status = await ChangeStatusAsync(member, item.Id, patched.Headers.ETag!.ToString(), "in_progress");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        var moved = await MoveAsync(member, item.Id, status.Headers.ETag!.ToString(), "in_review");
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        var comment = await AddCommentAsync(member, item.Id, "First");
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
        var commentBody = await comment.Content.ReadFromJsonAsync<JsonElement>();
        var commentId = commentBody.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await EditCommentAsync(member, commentId, commentBody.GetProperty("etag").GetString()!, "Edited")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(member, HttpMethod.Delete, $"/api/v1/comments/{commentId}")).StatusCode);

        var after = await GetAsync<JsonElement>(owner, $"/api/v1/work-items/{item.Id}");
        Assert.Equal("Renamed by a member", after.GetProperty("title").GetString());
        Assert.Equal("in_review", after.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_member_cannot_create_a_project()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var member = await JoinAsync(owner, "member");

        await AssertForbiddenAsync(await CreateProjectRawAsync(member, "MEMP"));

        var projects = await GetAsync<JsonElement>(owner, "/api/v1/projects");
        Assert.Empty(projects.GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("owner")]
    public async Task Admins_and_owners_create_projects_and_edit_work(string role)
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var user = await JoinAsync(owner, role);

        var project = await CreateProjectAsync(user, "ADM");
        var item = await CreateItemAsync(user, project, "By an admin");
        Assert.Equal(HttpStatusCode.Created, (await AddCommentAsync(user, item.Id, "Noted")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ChangeStatusAsync(user, item.Id, item.ETag, "in_progress")).StatusCode);
    }

    [Fact]
    public async Task The_workflow_rules_still_apply_to_a_member()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(owner, "FLOW");
        using var member = await JoinAsync(owner, "member");
        var item = await CreateItemAsync(member, project, "Skip ahead");

        var response = await ChangeStatusAsync(member, item.Id, item.ETag, "done");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task A_new_role_applies_from_the_next_token_refresh()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(owner, "DEM");
        using var member = await JoinAsync(owner, "member");
        Assert.Equal(HttpStatusCode.Created, (await CreateItemRawAsync(member, project, "Before")).StatusCode);

        await SetRoleAsync(member, "viewer");
        var demoted = await RefreshedAsync(member);

        Assert.Equal("viewer", AuthFlow.DecodeJwtPart(demoted.Tokens.AccessToken, 1).GetProperty("role").GetString());
        await AssertForbiddenAsync(await CreateItemRawAsync(demoted, project, "After"));
    }

    [Fact]
    public async Task A_user_cannot_belong_to_a_second_organization()
    {
        using var first = await AuthFlow.SignedUpUserAsync(factory, "First");
        using var second = await AuthFlow.SignedUpUserAsync(factory, "Second");
        var userId = (await GetAsync<MeBody>(first, "/api/v1/me")).User.Id;

        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO identity.memberships (org_id, user_id, role, created_at) VALUES (@org, @user, 'member', now())", connection);
        command.Parameters.AddWithValue("org", await OrgIdAsync(second));
        command.Parameters.AddWithValue("user", userId);

        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ux_memberships_user_id", error.ConstraintName);
    }

    /// <summary>
    /// Signs up a new user and moves them into <paramref name="owner"/>'s organization with <paramref name="role"/>
    /// (there is no invite yet, TREK-47), then gets them fresh tokens that carry the new organization and role.
    /// </summary>
    private async Task<SignedUpUser> JoinAsync(SignedUpUser owner, string role)
    {
        var user = await AuthFlow.SignedUpUserAsync(factory, "Left behind");
        var userId = (await GetAsync<MeBody>(user, "/api/v1/me")).User.Id;
        await ExecuteAsSuperuserAsync(
            "UPDATE identity.memberships SET org_id = @org, role = @role WHERE user_id = @user",
            ("org", await OrgIdAsync(owner)), ("role", role), ("user", userId));
        return user with { Tokens = await AuthFlow.GetTokensAsync(user.Client) };
    }

    /// <summary>The same user with tokens from a refresh, which carry their current role.</summary>
    private static async Task<SignedUpUser> RefreshedAsync(SignedUpUser user)
    {
        var refresh = await AuthFlow.RefreshAsync(user.Client, user.Tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        return user with { Tokens = (await refresh.Content.ReadFromJsonAsync<TokenResponse>())! };
    }

    private async Task SetRoleAsync(SignedUpUser user, string role)
    {
        var userId = (await GetAsync<MeBody>(user, "/api/v1/me")).User.Id;
        await ExecuteAsSuperuserAsync(
            "UPDATE identity.memberships SET role = @role WHERE user_id = @user", ("role", role), ("user", userId));
    }

    private async Task ExecuteAsSuperuserAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static async Task<Guid> OrgIdAsync(SignedUpUser user)
        => (await GetAsync<MeBody>(user, "/api/v1/me")).Organization.Id;

    private static async Task AssertForbiddenAsync(HttpResponseMessage response)
    {
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} answered {response.StatusCode}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal(403, problem?.Status);
    }

    private static async Task<T> GetAsync<T>(SignedUpUser user, string path)
    {
        var response = await SendAsync(user, HttpMethod.Get, path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    private static Task<HttpResponseMessage> CreateProjectRawAsync(SignedUpUser user, string key)
        => SendAsync(user, HttpMethod.Post, "/api/v1/projects", JsonContent.Create(new { name = $"Project {key}", key }));

    private static async Task<Guid> CreateProjectAsync(SignedUpUser user, string key)
    {
        var response = await CreateProjectRawAsync(user, key);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> CreateItemRawAsync(SignedUpUser user, Guid projectId, string title)
        => SendAsync(user, HttpMethod.Post, $"/api/v1/projects/{projectId}/work-items", JsonContent.Create(new { type = "task", title }));

    private static async Task<CreatedItem> CreateItemAsync(SignedUpUser user, Guid projectId, string title)
    {
        var response = await CreateItemRawAsync(user, projectId, title);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return new CreatedItem((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(), response.Headers.ETag!.ToString());
    }

    private static Task<HttpResponseMessage> PatchItemAsync(SignedUpUser user, Guid id, string etag, string json)
        => SendAsync(user, HttpMethod.Patch, $"/api/v1/work-items/{id}",
            new StringContent(json, Encoding.UTF8, "application/merge-patch+json"), etag);

    private static Task<HttpResponseMessage> ChangeStatusAsync(SignedUpUser user, Guid id, string etag, string to)
        => SendAsync(user, HttpMethod.Post, $"/api/v1/work-items/{id}/status", JsonContent.Create(new { to }), etag);

    private static Task<HttpResponseMessage> MoveAsync(SignedUpUser user, Guid id, string etag, string status)
        => SendAsync(user, HttpMethod.Post, $"/api/v1/work-items/{id}/move",
            JsonContent.Create(new { status, afterId = (Guid?)null, beforeId = (Guid?)null }), etag);

    private static Task<HttpResponseMessage> AddCommentAsync(SignedUpUser user, Guid itemId, string bodyMd)
        => SendAsync(user, HttpMethod.Post, $"/api/v1/work-items/{itemId}/comments", JsonContent.Create(new { bodyMd }));

    private static Task<HttpResponseMessage> EditCommentAsync(SignedUpUser user, Guid commentId, string etag, string bodyMd)
        => SendAsync(user, HttpMethod.Patch, $"/api/v1/comments/{commentId}",
            new StringContent(JsonSerializer.Serialize(new { bodyMd }), Encoding.UTF8, "application/merge-patch+json"), etag);

    private sealed record CreatedItem(Guid Id, string ETag);
}
