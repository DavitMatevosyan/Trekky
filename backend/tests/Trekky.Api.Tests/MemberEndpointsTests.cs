using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Trekky.Api.Tests;

/// <summary>
/// TREK-48: everyone sees the organization's members; Admins change Members' and Viewers' roles and remove them, only an
/// Owner makes or removes Admins and Owners, and the last Owner stays. A changed or removed member's tokens stop working on
/// their next request, and a removed member's name stays on their past work.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MemberEndpointsTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task Every_member_sees_each_members_name_email_role_kind_and_etag()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var viewer = await InviteAndAcceptAsync(owner, "viewer", "Vera Viewer");

        var members = await MembersAsync(viewer);

        Assert.Equal(2, members.Count);
        var vera = members.Single(m => m.User.Email == viewer.Email);
        Assert.Equal(("Vera Viewer", "viewer", "person"), (vera.User.DisplayName, vera.Role, vera.Kind));
        Assert.Matches("^\"\\d+\"$", vera.Etag);
        Assert.Equal(("owner", "person"), (Find(members, owner.Email).Role, Find(members, owner.Email).Kind));
    }

    [Fact]
    public async Task An_admin_changes_members_and_viewers_but_only_an_owner_touches_admins_and_owners()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var admin = await InviteAndAcceptAsync(owner, "admin");
        using var member = await InviteAndAcceptAsync(owner, "member");
        using var otherAdmin = await InviteAndAcceptAsync(owner, "admin");

        var changed = await ChangeRoleAsync(admin, await MemberAsync(owner, member.Email), "viewer");
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var body = (await changed.Content.ReadFromJsonAsync<MemberBody>())!;
        Assert.Equal("viewer", body.Role);
        Assert.Equal(body.Etag, changed.Headers.ETag?.ToString());

        await AssertProblemAsync(await ChangeRoleAsync(admin, await MemberAsync(owner, member.Email), "admin"), HttpStatusCode.Forbidden, "Only an Owner");
        await AssertProblemAsync(await ChangeRoleAsync(admin, await MemberAsync(owner, otherAdmin.Email), "member"), HttpStatusCode.Forbidden, "Only an Owner");
        await AssertProblemAsync(await ChangeRoleAsync(admin, await MemberAsync(owner, owner.Email), "admin"), HttpStatusCode.Forbidden, "Only an Owner");
        await AssertProblemAsync(await RemoveAsync(admin, otherAdmin), HttpStatusCode.Forbidden, "Only an Owner");
        await AssertProblemAsync(await RemoveAsync(admin, owner), HttpStatusCode.Forbidden, "Only an Owner");

        Assert.Equal(HttpStatusCode.OK, (await ChangeRoleAsync(owner, await MemberAsync(owner, member.Email), "admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ChangeRoleAsync(owner, await MemberAsync(owner, otherAdmin.Email), "owner")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RemoveAsync(owner, admin)).StatusCode);

        var roles = (await MembersAsync(owner)).ToDictionary(m => m.User.Email!, m => m.Role);
        Assert.Equal(
            new Dictionary<string, string> { [owner.Email] = "owner", [member.Email] = "admin", [otherAdmin.Email] = "owner" },
            roles);
    }

    [Theory]
    [InlineData("member")]
    [InlineData("viewer")]
    public async Task Members_and_viewers_cannot_change_or_remove_anyone(string role)
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var user = await InviteAndAcceptAsync(owner, role);
        using var other = await InviteAndAcceptAsync(owner, "viewer");

        await AssertProblemAsync(await ChangeRoleAsync(user, await MemberAsync(owner, other.Email), "member"), HttpStatusCode.Forbidden);
        await AssertProblemAsync(await ChangeRoleAsync(user, await MemberAsync(owner, user.Email), "admin"), HttpStatusCode.Forbidden);
        await AssertProblemAsync(await RemoveAsync(user, other), HttpStatusCode.Forbidden);
        Assert.Equal(3, (await MembersAsync(owner)).Count);
    }

    [Fact]
    public async Task The_last_owner_can_be_neither_demoted_nor_removed()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);

        await AssertProblemAsync(await ChangeRoleAsync(owner, await MemberAsync(owner, owner.Email), "admin"), HttpStatusCode.Conflict, "at least one Owner");
        await AssertProblemAsync(await RemoveAsync(owner, owner), HttpStatusCode.Conflict, "at least one Owner");

        // With a second Owner, either may step down.
        using var second = await InviteAndAcceptAsync(owner, "admin");
        Assert.Equal(HttpStatusCode.OK, (await ChangeRoleAsync(owner, await MemberAsync(owner, second.Email), "owner")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ChangeRoleAsync(owner, await MemberAsync(owner, owner.Email), "member")).StatusCode);
        using var secondAsOwner = await SignInAgainAsync(second);
        await AssertProblemAsync(await RemoveAsync(secondAsOwner, second), HttpStatusCode.Conflict, "at least one Owner");
    }

    [Fact]
    public async Task Two_owners_demoting_each_other_at_once_leave_one_owner()
    {
        using var first = await AuthFlow.SignedUpUserAsync(factory);
        using var second = await InviteAndAcceptAsync(first, "admin");
        Assert.Equal(HttpStatusCode.OK, (await ChangeRoleAsync(first, await MemberAsync(first, second.Email), "owner")).StatusCode);
        using var secondAsOwner = await SignInAgainAsync(second);
        var firstMember = await MemberAsync(first, first.Email);
        var secondMember = await MemberAsync(first, second.Email);
        var orgId = (await MeAsync(first)).Organization.Id;

        var results = await Task.WhenAll(
            ChangeRoleAsync(first, secondMember, "member"),
            ChangeRoleAsync(secondAsOwner, firstMember, "member"));

        // The loser waits for the winner's lock and is then no Owner any more (403), or was already signed out by it (401).
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM identity.memberships WHERE org_id = @org AND role = 'owner' AND removed_at IS NULL",
            ("org", orgId)));
    }

    [Fact]
    public async Task A_caller_whose_role_was_lowered_meanwhile_is_judged_by_the_role_they_have_now()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var other = await InviteAndAcceptAsync(owner, "admin");
        using var third = await InviteAndAcceptAsync(owner, "admin");
        Assert.Equal(HttpStatusCode.OK, (await ChangeRoleAsync(owner, await MemberAsync(owner, other.Email), "owner")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ChangeRoleAsync(owner, await MemberAsync(owner, third.Email), "owner")).StatusCode);
        using var demoted = await SignInAgainAsync(other);
        var target = await MemberAsync(owner, third.Email);

        // As if a demotion committed while this caller's request was already past authentication: their token still says Owner.
        await ExecuteAsSuperuserAsync("UPDATE identity.memberships SET role = 'member' WHERE user_id = @id", ("id", IdOf(other)));

        await AssertProblemAsync(await ChangeRoleAsync(demoted, target, "viewer"), HttpStatusCode.Forbidden, "Only an Owner");
        await AssertProblemAsync(await RemoveAsync(demoted, third), HttpStatusCode.Forbidden, "Only an Owner");
        Assert.Equal("owner", (await MemberAsync(owner, third.Email)).Role);
    }

    [Fact]
    public async Task A_role_change_needs_the_members_current_etag()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var member = await InviteAndAcceptAsync(owner, "member");
        var before = await MemberAsync(owner, member.Email);

        await AssertProblemAsync(await ChangeRoleAsync(owner, before, "viewer", ifMatch: null), (HttpStatusCode)428, "If-Match");
        Assert.Equal(HttpStatusCode.OK, (await ChangeRoleAsync(owner, before, "viewer")).StatusCode);

        var stale = await ChangeRoleAsync(owner, before, "admin");
        await AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, "changed since");
        var latest = await MemberAsync(owner, member.Email);
        Assert.Equal(latest.Etag, stale.Headers.ETag?.ToString());
        Assert.Equal("viewer", latest.Role);
    }

    [Fact]
    public async Task Giving_the_same_role_again_changes_nothing_and_keeps_the_members_tokens()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var member = await InviteAndAcceptAsync(owner, "member");
        var before = await MemberAsync(owner, member.Email);

        var response = await ChangeRoleAsync(owner, before, "member");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before.Etag, response.Headers.ETag?.ToString());
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(member, HttpMethod.Get, "/api/v1/me")).StatusCode);
        Assert.Empty(await EventsAsync(member));
    }

    [Fact]
    public async Task After_a_role_change_the_members_old_tokens_stop_and_signing_in_again_brings_the_new_role()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var member = await InviteAndAcceptAsync(owner, "viewer");

        Assert.Equal(HttpStatusCode.OK, (await ChangeRoleAsync(owner, await MemberAsync(owner, member.Email), "member")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(member, HttpMethod.Get, "/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthFlow.RefreshAsync(member.Client, member.Tokens.RefreshToken)).StatusCode);

        // The session cookie is kept, so the web app's silent sign-in gets a token with the new role.
        var tokens = await AuthFlow.GetTokensAsync(member.Client);
        Assert.Equal("member", AuthFlow.DecodeJwtPart(tokens.AccessToken, 1).GetProperty("role").GetString());
    }

    [Fact]
    public async Task A_removed_member_is_signed_out_everywhere_and_cannot_log_in_again()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var member = await InviteAndAcceptAsync(owner, "member");

        Assert.Equal(HttpStatusCode.NoContent, (await RemoveAsync(owner, member)).StatusCode);

        Assert.DoesNotContain(await MembersAsync(owner), m => m.User.Email == member.Email);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(member, HttpMethod.Get, "/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthFlow.RefreshAsync(member.Client, member.Tokens.RefreshToken)).StatusCode);

        // The session cookie no longer signs them in: authorize sends them to the login page, which refuses them.
        var authorize = await AuthFlow.AuthorizeAsync(member.Client, Challenge());
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        Assert.StartsWith("/login?", authorize.Headers.Location?.OriginalString, StringComparison.Ordinal);
        using var fresh = factory.CreateBrowserClient();
        var login = await AuthFlow.LogInAsync(fresh, member.Email);
        await AssertProblemAsync(login, HttpStatusCode.Forbidden, "no longer belongs to an organization");
        var afterLogin = await AuthFlow.AuthorizeAsync(fresh, Challenge());
        Assert.StartsWith("/login?", afterLogin.Headers.Location?.OriginalString, StringComparison.Ordinal);

        // Removed once is removed: a second removal finds nobody.
        await AssertProblemAsync(await RemoveAsync(owner, member), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_removed_members_past_work_keeps_their_name_but_they_get_no_new_work()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var member = await InviteAndAcceptAsync(owner, "member", "Rita Removed");
        var memberId = IdOf(member);
        var project = await CreateProjectAsync(owner, "RMV");
        var item = await CreateItemAsync(member, project, "Done before leaving");
        await WaitForHistoryAsync(owner, item.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await RemoveAsync(owner, member)).StatusCode);

        var detail = await GetAsync<JsonElement>(owner, $"/api/v1/work-items/{item.Id}");
        Assert.Equal("Rita Removed", detail.GetProperty("createdBy").GetProperty("displayName").GetString());
        var history = await WaitForHistoryAsync(owner, item.Id);
        Assert.Equal("Rita Removed", history[^1].GetProperty("actor").GetProperty("displayName").GetString());

        var etag = (await SendAsync(owner, HttpMethod.Get, $"/api/v1/work-items/{item.Id}")).Headers.ETag!.ToString();
        using var assign = AuthFlow.WithBearer(HttpMethod.Patch, $"/api/v1/work-items/{item.Id}", owner.Tokens.AccessToken);
        assign.Content = new StringContent($$"""{ "assigneeId": "{{memberId}}" }""", Encoding.UTF8, "application/merge-patch+json");
        assign.Headers.TryAddWithoutValidation("If-Match", etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.Client.SendAsync(assign)).StatusCode);
    }

    [Fact]
    public async Task A_removed_members_email_cannot_be_invited_again_yet()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var member = await InviteAndAcceptAsync(owner, "member");
        Assert.Equal(HttpStatusCode.NoContent, (await RemoveAsync(owner, member)).StatusCode);

        var invite = await SendAsync(owner, HttpMethod.Post, "/api/v1/invites", JsonContent.Create(new { email = member.Email, role = "member" }));

        await AssertProblemAsync(invite, HttpStatusCode.Conflict, "was removed from your organization");
    }

    [Fact]
    public async Task Role_changes_and_removals_record_activity_events()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var member = await InviteAndAcceptAsync(owner, "viewer");
        var ownerId = IdOf(owner);
        var memberId = IdOf(member);

        Assert.Equal(HttpStatusCode.OK, (await ChangeRoleAsync(owner, await MemberAsync(owner, member.Email), "member")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RemoveAsync(owner, member)).StatusCode);

        Assert.Equal(["MemberRoleChanged viewer→member", "MemberRemoved member"], await EventsAsync(member));
        var stored = await WaitForActivityAsync(memberId, count: 2);
        Assert.All(stored, e => Assert.Equal(("member", ownerId), (e.EntityType, e.ActorId)));
    }

    [Fact]
    public async Task Another_organizations_members_cannot_be_changed_or_removed()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var stranger = await AuthFlow.SignedUpUserAsync(factory, "Strangers");
        var target = await MemberAsync(owner, owner.Email);

        await AssertProblemAsync(await ChangeRoleAsync(stranger, target, "viewer"), HttpStatusCode.NotFound);
        await AssertProblemAsync(await RemoveAsync(stranger, owner), HttpStatusCode.NotFound);
        Assert.Equal("owner", (await MemberAsync(owner, owner.Email)).Role);
    }

    [Theory]
    [InlineData("boss")]
    [InlineData("")]
    [InlineData(null)]
    public async Task An_unknown_role_is_rejected(string? role)
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var member = await InviteAndAcceptAsync(owner, "member");

        var response = await ChangeRoleAsync(owner, await MemberAsync(owner, member.Email), role);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<ProblemBody>())?.Errors?.ContainsKey("role"));
    }

    /// <summary>Invites a new address into <paramref name="owner"/>'s organization and accepts it; returns the new member with tokens.</summary>
    private async Task<SignedUpUser> InviteAndAcceptAsync(SignedUpUser owner, string role, string? displayName = null)
    {
        var email = AuthFlow.NewEmail();
        var created = await SendAsync(owner, HttpMethod.Post, "/api/v1/invites", JsonContent.Create(new { email, role }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var acceptUrl = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("acceptUrl").GetString()!;
        var client = factory.CreateBrowserClient();
        var accepted = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/invites/accept", UriKind.Relative),
            new { token = new Uri(acceptUrl).Fragment.TrimStart('#'), password = AuthFlow.Password, displayName });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        return new SignedUpUser(client, email, AuthFlow.SessionCookie(accepted), await AuthFlow.GetTokensAsync(client));
    }

    /// <summary>The same user with fresh tokens from their session cookie, e.g. after their role changed.</summary>
    private static async Task<SignedUpUser> SignInAgainAsync(SignedUpUser user)
        => user with { Tokens = await AuthFlow.GetTokensAsync(user.Client) };

    private static async Task<List<MemberBody>> MembersAsync(SignedUpUser user)
        => (await GetAsync<MemberListBody>(user, "/api/v1/members")).Items;

    private static async Task<MemberBody> MemberAsync(SignedUpUser reader, string email) => Find(await MembersAsync(reader), email);

    private static MemberBody Find(List<MemberBody> members, string email)
        => members.Single(m => string.Equals(m.User.Email, email, StringComparison.OrdinalIgnoreCase));

    private static Task<HttpResponseMessage> ChangeRoleAsync(SignedUpUser actor, MemberBody target, string? role)
        => ChangeRoleAsync(actor, target, role, target.Etag);

    private static Task<HttpResponseMessage> ChangeRoleAsync(SignedUpUser actor, MemberBody target, string? role, string? ifMatch)
    {
        var request = AuthFlow.WithBearer(HttpMethod.Patch, $"/api/v1/members/{target.User.Id}", actor.Tokens.AccessToken);
        request.Content = JsonContent.Create(new { role });
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return actor.Client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> RemoveAsync(SignedUpUser actor, SignedUpUser target)
        => SendAsync(actor, HttpMethod.Delete, $"/api/v1/members/{IdOf(target)}");

    /// <summary>The user's id, from their access token (still readable after the token stops working).</summary>
    private static Guid IdOf(SignedUpUser user)
        => Guid.Parse(AuthFlow.DecodeJwtPart(user.Tokens.AccessToken, 1).GetProperty("sub").GetString()!);

    private static Task<MeBody> MeAsync(SignedUpUser user) => GetAsync<MeBody>(user, "/api/v1/me");

    private static async Task<Guid> CreateProjectAsync(SignedUpUser user, string key)
    {
        var response = await SendAsync(user, HttpMethod.Post, "/api/v1/projects", JsonContent.Create(new { name = $"Project {key}", key }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<(Guid Id, string ETag)> CreateItemAsync(SignedUpUser user, Guid projectId, string title)
    {
        var response = await SendAsync(user, HttpMethod.Post, $"/api/v1/projects/{projectId}/work-items", JsonContent.Create(new { type = "task", title }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(), response.Headers.ETag!.ToString());
    }

    /// <summary>The item's history, newest first, once its creation has arrived through the outbox.</summary>
    private static async Task<List<JsonElement>> WaitForHistoryAsync(SignedUpUser user, Guid itemId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var page = await GetAsync<JsonElement>(user, $"/api/v1/work-items/{itemId}/history");
            var items = page.GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToList();
            if (items.Count > 0)
            {
                return items;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"No history for {itemId} within 10 seconds.");
    }

    /// <summary>The member events written to the identity outbox for <paramref name="member"/>, oldest first.</summary>
    private async Task<List<string>> EventsAsync(SignedUpUser member)
    {
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT event_type || ' ' || COALESCE(envelope->'payload'->>'from' || '→' || (envelope->'payload'->>'to'), envelope->'payload'->>'role') " +
            "FROM identity.outbox WHERE envelope->'entity'->>'type' = 'member' AND envelope->'payload'->>'email' = @email ORDER BY id",
            connection);
        command.Parameters.AddWithValue("email", member.Email);
        var events = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            events.Add(reader.GetString(0));
        }

        return events;
    }

    private async Task<List<(string EntityType, Guid? ActorId)>> WaitForActivityAsync(Guid entityId, int count)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT entity_type, actor_id FROM activity.activity_events WHERE entity_id = @id AND entity_type = 'member' ORDER BY event_id",
                connection);
            command.Parameters.AddWithValue("id", entityId);
            var rows = new List<(string, Guid?)>();
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    rows.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetGuid(1)));
                }
            }

            if (rows.Count >= count)
            {
                return rows;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Fewer than {count} activity events arrived for {entityId}.");
    }

    private async Task<long> CountAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (long)(await command.ExecuteScalarAsync())!;
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

    private static string Challenge()
        => Convert.ToBase64String(SHA256.HashData(RandomNumberGenerator.GetBytes(32))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string? detailContains = null)
    {
        Assert.True(
            response.StatusCode == status,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} answered {response.StatusCode}, not {status}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        if (detailContains is not null)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
            Assert.Contains(detailContains, problem?.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<T> GetAsync<T>(SignedUpUser user, string path)
    {
        var response = await SendAsync(user, HttpMethod.Get, path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static Task<HttpResponseMessage> SendAsync(SignedUpUser user, HttpMethod method, string path, HttpContent? content = null)
    {
        var request = AuthFlow.WithBearer(method, path, user.Tokens.AccessToken);
        request.Content = content;
        return user.Client.SendAsync(request);
    }

    private sealed record MemberBody(MeUser User, string Role, string Kind, string Etag);

    private sealed record MemberListBody(List<MemberBody> Items);
}
