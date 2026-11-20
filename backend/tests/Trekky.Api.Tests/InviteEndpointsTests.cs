using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Trekky.Api.Tests;

/// <summary>
/// TREK-47: Admins and Owners invite people by email with a role (ADR-0027). The link is single-use and expires; accepting
/// it creates the account inside the inviter's organization with the invited role, never a new organization.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class InviteEndpointsTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task An_owner_invites_someone_who_accepts_and_lands_in_the_owners_organization_with_the_invited_role()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory, "Invite Co");
        var email = AuthFlow.NewEmail();

        var created = await CreateInviteAsync(owner, email, "viewer");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var sent = await created.Content.ReadFromJsonAsync<SentInviteBody>();
        Assert.NotNull(sent);
        Assert.Equal(email, sent.Invite.Email);
        Assert.Equal("viewer", sent.Invite.Role);
        Assert.Equal("pending", sent.Invite.Status);
        Assert.False(sent.EmailSent); // no SMTP server in tests
        Assert.StartsWith("http://localhost:5173/invite#", sent.AcceptUrl, StringComparison.Ordinal);
        Assert.InRange(sent.Invite.ExpiresAt - sent.Invite.SentAt, TimeSpan.FromDays(7) - TimeSpan.FromSeconds(1), TimeSpan.FromDays(7));

        var listed = await GetAsync<InviteListBody>(owner, "/api/v1/invites");
        Assert.Equal([email], listed.Items.Select(i => i.Email));
        Assert.Equal((await MeAsync(owner)).User.DisplayName, listed.Items[0].InvitedBy?.DisplayName);

        using var invitee = factory.CreateBrowserClient();
        var token = Token(sent.AcceptUrl);
        var preview = await PreviewAsync(invitee, token);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, previewBody.GetProperty("email").GetString());
        Assert.Equal("viewer", previewBody.GetProperty("role").GetString());
        Assert.Equal("Invite Co", previewBody.GetProperty("organization").GetProperty("name").GetString());

        var accepted = await AcceptAsync(invitee, token, displayName: "Ines Invited");
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        var me = await accepted.Content.ReadFromJsonAsync<MeBody>();
        Assert.Equal("viewer", me!.Role);
        Assert.Equal((await MeAsync(owner)).Organization.Id, me.Organization.Id);
        Assert.Equal("Ines Invited", me.User.DisplayName);

        // The session cookie from accepting runs the code flow, as after sign-up; the token carries the invited role.
        var tokens = await AuthFlow.GetTokensAsync(invitee);
        var payload = AuthFlow.DecodeJwtPart(tokens.AccessToken, 1);
        Assert.Equal("viewer", payload.GetProperty("role").GetString());
        Assert.Equal(me.Organization.Id.ToString(), payload.GetProperty("org_id").GetString());

        var members = await GetAsync<JsonElement>(owner, "/api/v1/members");
        Assert.Contains(members.GetProperty("items").EnumerateArray(), m => m.GetProperty("user").GetProperty("email").GetString() == email);
        Assert.Empty((await GetAsync<InviteListBody>(owner, "/api/v1/invites")).Items);
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.organizations WHERE name = 'Invite Co' AND id = @org", ("org", me.Organization.Id)));
    }

    [Fact]
    public async Task A_link_works_once()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var sent = await SentAsync(owner, AuthFlow.NewEmail(), "member");
        using var invitee = factory.CreateBrowserClient();
        Assert.Equal(HttpStatusCode.Created, (await AcceptAsync(invitee, Token(sent.AcceptUrl))).StatusCode);

        using var someoneElse = factory.CreateBrowserClient();
        await AssertProblemAsync(await PreviewAsync(someoneElse, Token(sent.AcceptUrl)), HttpStatusCode.Gone, "already been used");
        await AssertProblemAsync(await AcceptAsync(someoneElse, Token(sent.AcceptUrl)), HttpStatusCode.Gone, "already been used");
    }

    [Fact]
    public async Task An_admin_invites_members_and_viewers_but_only_an_owner_invites_admins()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var admin = await InviteAndAcceptAsync(owner, "admin");

        Assert.Equal(HttpStatusCode.Created, (await CreateInviteAsync(admin, AuthFlow.NewEmail(), "member")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CreateInviteAsync(admin, AuthFlow.NewEmail(), "viewer")).StatusCode);
        await AssertProblemAsync(await CreateInviteAsync(admin, AuthFlow.NewEmail(), "admin"), HttpStatusCode.Forbidden, "Only an Owner");

        // Nor may an admin resend or withdraw an Owner's invite for an Admin.
        var adminInvite = await SentAsync(owner, AuthFlow.NewEmail(), "admin");
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, $"/api/v1/invites/{adminInvite.Invite.Id}/resend"), HttpStatusCode.Forbidden, "Only an Owner");
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Delete, $"/api/v1/invites/{adminInvite.Invite.Id}"), HttpStatusCode.Forbidden, "Only an Owner");
    }

    [Theory]
    [InlineData("member")]
    [InlineData("viewer")]
    public async Task Members_and_viewers_cannot_see_or_send_invites(string role)
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var pending = await SentAsync(owner, AuthFlow.NewEmail(), "viewer");
        using var user = await InviteAndAcceptAsync(owner, role);

        await AssertProblemAsync(await SendAsync(user, HttpMethod.Get, "/api/v1/invites"), HttpStatusCode.Forbidden);
        await AssertProblemAsync(await CreateInviteAsync(user, AuthFlow.NewEmail(), "viewer"), HttpStatusCode.Forbidden);
        await AssertProblemAsync(await SendAsync(user, HttpMethod.Post, $"/api/v1/invites/{pending.Invite.Id}/resend"), HttpStatusCode.Forbidden);
        await AssertProblemAsync(await SendAsync(user, HttpMethod.Delete, $"/api/v1/invites/{pending.Invite.Id}"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_email_that_already_has_an_account_cannot_be_invited()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var elsewhere = await AuthFlow.SignedUpUserAsync(factory, "Elsewhere");
        using var member = await InviteAndAcceptAsync(owner, "member");

        await AssertProblemAsync(await CreateInviteAsync(owner, elsewhere.Email.ToUpperInvariant(), "member"), HttpStatusCode.Conflict, "another organization");
        await AssertProblemAsync(await CreateInviteAsync(owner, member.Email, "member"), HttpStatusCode.Conflict, "already a member");
        await AssertProblemAsync(await CreateInviteAsync(owner, owner.Email, "member"), HttpStatusCode.Conflict, "already a member");
    }

    [Fact]
    public async Task An_email_with_an_open_invite_is_resent_not_invited_twice()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var email = AuthFlow.NewEmail();
        await SentAsync(owner, email, "member");

        await AssertProblemAsync(await CreateInviteAsync(owner, $"  {email.ToUpperInvariant()} ", "viewer"), HttpStatusCode.Conflict, "Resend it instead");
        Assert.Single((await GetAsync<InviteListBody>(owner, "/api/v1/invites")).Items);
    }

    [Fact]
    public async Task Accepting_fails_when_the_email_got_an_account_in_the_meantime()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var email = AuthFlow.NewEmail();
        var sent = await SentAsync(owner, email, "member");
        using (var signUp = factory.CreateBrowserClient())
        {
            Assert.Equal(HttpStatusCode.Created, (await AuthFlow.SignUpAsync(signUp, email, "Own Org")).StatusCode);
        }

        using var invitee = factory.CreateBrowserClient();
        await AssertProblemAsync(await AcceptAsync(invitee, Token(sent.AcceptUrl)), HttpStatusCode.Conflict, "Log in instead");
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM identity.memberships m JOIN identity.users u ON u.id = m.user_id WHERE u.email = @email", ("email", email)));
    }

    [Fact]
    public async Task Resending_replaces_the_link_and_restarts_the_expiry()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var first = await SentAsync(owner, AuthFlow.NewEmail(), "member");
        await ExecuteAsSuperuserAsync(
            "UPDATE identity.invites SET sent_at = now() - interval '8 days', expires_at = now() - interval '1 day' WHERE id = @id",
            ("id", first.Invite.Id));
        using var invitee = factory.CreateBrowserClient();
        await AssertProblemAsync(await PreviewAsync(invitee, Token(first.AcceptUrl)), HttpStatusCode.Gone, "expired");
        Assert.Equal("expired", (await GetAsync<InviteListBody>(owner, "/api/v1/invites")).Items.Single().Status);

        var resent = await SendAsync(owner, HttpMethod.Post, $"/api/v1/invites/{first.Invite.Id}/resend");
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        var second = (await resent.Content.ReadFromJsonAsync<SentInviteBody>())!;
        Assert.NotEqual(first.AcceptUrl, second.AcceptUrl);
        Assert.Equal("pending", second.Invite.Status);
        Assert.True(second.Invite.ExpiresAt > DateTimeOffset.UtcNow.AddDays(6));

        await AssertProblemAsync(await PreviewAsync(invitee, Token(first.AcceptUrl)), HttpStatusCode.NotFound, "newer invite email");
        Assert.Equal(HttpStatusCode.Created, (await AcceptAsync(invitee, Token(second.AcceptUrl))).StatusCode);
    }

    [Fact]
    public async Task A_withdrawn_invite_says_so_and_cannot_be_changed_again()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var sent = await SentAsync(owner, AuthFlow.NewEmail(), "member");

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(owner, HttpMethod.Delete, $"/api/v1/invites/{sent.Invite.Id}")).StatusCode);

        using var invitee = factory.CreateBrowserClient();
        await AssertProblemAsync(await PreviewAsync(invitee, Token(sent.AcceptUrl)), HttpStatusCode.Gone, "withdrawn");
        await AssertProblemAsync(await AcceptAsync(invitee, Token(sent.AcceptUrl)), HttpStatusCode.Gone, "withdrawn");
        await AssertProblemAsync(await SendAsync(owner, HttpMethod.Post, $"/api/v1/invites/{sent.Invite.Id}/resend"), HttpStatusCode.Conflict);
        await AssertProblemAsync(await SendAsync(owner, HttpMethod.Delete, $"/api/v1/invites/{sent.Invite.Id}"), HttpStatusCode.Conflict);
        Assert.Empty((await GetAsync<InviteListBody>(owner, "/api/v1/invites")).Items);

        // Withdrawn invites no longer block a new one for the same address.
        Assert.Equal(HttpStatusCode.Created, (await CreateInviteAsync(owner, sent.Invite.Email, "viewer")).StatusCode);
    }

    [Fact]
    public async Task An_accept_that_races_a_withdrawal_loses_and_leaves_nothing_behind()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var sent = await SentAsync(owner, AuthFlow.NewEmail(), "member");

        // A withdrawal that has written but not yet committed: the accept reads the invite as open, then waits on its row.
        await using var revoker = new NpgsqlConnection(factory.SuperuserConnectionString);
        await revoker.OpenAsync();
        await using var revoke = await revoker.BeginTransactionAsync();
        await using (var update = new NpgsqlCommand("UPDATE identity.invites SET revoked_at = now() WHERE id = @id", revoker, revoke))
        {
            update.Parameters.AddWithValue("id", sent.Invite.Id);
            Assert.Equal(1, await update.ExecuteNonQueryAsync());
        }

        using var invitee = factory.CreateBrowserClient();
        var accepting = AcceptAsync(invitee, Token(sent.AcceptUrl));
        await WaitForLockWaitAsync();
        await revoke.CommitAsync();

        await AssertProblemAsync(await accepting, HttpStatusCode.Gone, "withdrawn");
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM identity.users WHERE email = @email", ("email", sent.Invite.Email)));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM identity.invites WHERE id = @id AND accepted_at IS NOT NULL", ("id", sent.Invite.Id)));
        Assert.Equal(["InviteSent:False"], await EventsAsync(sent.Invite.Id));
    }

    [Fact]
    public async Task Another_organizations_invites_are_invisible()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        using var stranger = await AuthFlow.SignedUpUserAsync(factory, "Strangers");
        var sent = await SentAsync(owner, AuthFlow.NewEmail(), "member");

        Assert.Empty((await GetAsync<InviteListBody>(stranger, "/api/v1/invites")).Items);
        await AssertProblemAsync(await SendAsync(stranger, HttpMethod.Post, $"/api/v1/invites/{sent.Invite.Id}/resend"), HttpStatusCode.NotFound);
        await AssertProblemAsync(await SendAsync(stranger, HttpMethod.Delete, $"/api/v1/invites/{sent.Invite.Id}"), HttpStatusCode.NotFound);
        Assert.Single((await GetAsync<InviteListBody>(owner, "/api/v1/invites")).Items);
    }

    [Fact]
    public async Task Without_a_tenant_the_app_role_sees_only_the_invite_whose_token_hash_it_presents()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var first = await SentAsync(owner, AuthFlow.NewEmail(), "member");
        await SentAsync(owner, AuthFlow.NewEmail(), "member");
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(Token(first.AcceptUrl))));

        await using var connection = new NpgsqlConnection(factory.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        Assert.Equal(0L, await ScalarAsync(connection, transaction, "SELECT count(*) FROM identity.invites"));

        await ScalarAsync(connection, transaction, "SELECT set_config('app.invite_token_hash', @hash, true)", ("hash", hash));
        Assert.Equal(first.Invite.Id, await ScalarAsync(connection, transaction, "SELECT id FROM identity.invites"));

        // The token policy only reads: changing the invite still needs its organization.
        Assert.Equal(0, await ExecuteAsync(connection, transaction, "UPDATE identity.invites SET revoked_at = now()"));
    }

    [Fact]
    public async Task Sending_withdrawing_and_accepting_invites_record_activity_events()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var withdrawn = await SentAsync(owner, AuthFlow.NewEmail(), "member");
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(owner, HttpMethod.Post, $"/api/v1/invites/{withdrawn.Invite.Id}/resend")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(owner, HttpMethod.Delete, $"/api/v1/invites/{withdrawn.Invite.Id}")).StatusCode);
        var accepted = await SentAsync(owner, AuthFlow.NewEmail(), "viewer");
        using var invitee = factory.CreateBrowserClient();
        var me = await (await AcceptAsync(invitee, Token(accepted.AcceptUrl))).Content.ReadFromJsonAsync<MeBody>();

        Assert.Equal(
            ["InviteSent:False", "InviteSent:True", "InviteRevoked:"],
            await EventsAsync(withdrawn.Invite.Id));
        Assert.Equal(["InviteSent:False", "InviteAccepted:"], await EventsAsync(accepted.Invite.Id));

        // Events reach the activity log, the accept one with the new member as its actor; no token or hash in any of them.
        var stored = await WaitForActivityAsync(accepted.Invite.Id, count: 2);
        Assert.Contains(stored, e => e.Type == "InviteAccepted" && e.ActorId == me!.User.Id);
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM identity.outbox WHERE envelope::text ILIKE '%token%'"));
    }

    [Theory]
    [InlineData("not-an-email", "member", "email")]
    [InlineData("", "member", "email")]
    [InlineData("someone@example.com", "owner", "role")]
    [InlineData("someone@example.com", "boss", "role")]
    [InlineData("someone@example.com", null, "role")]
    public async Task Invalid_invites_are_rejected_per_field(string email, string? role, string field)
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var response = await SendAsync(owner, HttpMethod.Post, "/api/v1/invites", JsonContent.Create(new { email, role }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.True(problem?.Errors?.ContainsKey(field), $"No error for {field}");
    }

    [Fact]
    public async Task Accepting_needs_a_valid_password_and_a_real_token()
    {
        using var owner = await AuthFlow.SignedUpUserAsync(factory);
        var sent = await SentAsync(owner, AuthFlow.NewEmail(), "member");
        using var invitee = factory.CreateBrowserClient();

        var shortPassword = await AcceptAsync(invitee, Token(sent.AcceptUrl), password: "too short");
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);
        Assert.True((await shortPassword.Content.ReadFromJsonAsync<ProblemBody>())?.Errors?.ContainsKey("password"));

        await AssertProblemAsync(await AcceptAsync(invitee, "made-up"), HttpStatusCode.NotFound);
        await AssertProblemAsync(await AcceptAsync(invitee, new string('A', 43)), HttpStatusCode.NotFound);
        await AssertProblemAsync(await PreviewAsync(invitee, string.Empty), HttpStatusCode.NotFound);

        // Nothing was used up: the real link still works.
        Assert.Equal(HttpStatusCode.Created, (await AcceptAsync(invitee, Token(sent.AcceptUrl))).StatusCode);
    }

    /// <summary>Invites a new address into <paramref name="owner"/>'s organization and accepts it; returns the new member with tokens.</summary>
    private async Task<SignedUpUser> InviteAndAcceptAsync(SignedUpUser owner, string role)
    {
        var email = AuthFlow.NewEmail();
        var sent = await SentAsync(owner, email, role);
        var client = factory.CreateBrowserClient();
        var accepted = await AcceptAsync(client, Token(sent.AcceptUrl));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        return new SignedUpUser(client, email, AuthFlow.SessionCookie(accepted), await AuthFlow.GetTokensAsync(client));
    }

    private static async Task<SentInviteBody> SentAsync(SignedUpUser user, string email, string role)
    {
        var response = await CreateInviteAsync(user, email, role);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SentInviteBody>())!;
    }

    private static Task<HttpResponseMessage> CreateInviteAsync(SignedUpUser user, string email, string role)
        => SendAsync(user, HttpMethod.Post, "/api/v1/invites", JsonContent.Create(new { email, role }));

    private static Task<HttpResponseMessage> PreviewAsync(HttpClient client, string token)
        => client.PostAsJsonAsync(new Uri("/api/v1/auth/invites/preview", UriKind.Relative), new { token });

    private static Task<HttpResponseMessage> AcceptAsync(
        HttpClient client, string token, string password = AuthFlow.Password, string? displayName = null)
        => client.PostAsJsonAsync(new Uri("/api/v1/auth/invites/accept", UriKind.Relative), new { token, password, displayName });

    /// <summary>The token is the link's fragment: <c>…/invite#&lt;token&gt;</c>.</summary>
    private static string Token(string acceptUrl) => new Uri(acceptUrl).Fragment.TrimStart('#');

    private async Task<List<string>> EventsAsync(Guid inviteId)
    {
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT event_type || ':' || COALESCE(envelope->'payload'->>'resent', '') FROM identity.outbox " +
            "WHERE envelope->'entity'->>'id' = @id ORDER BY id",
            connection);
        command.Parameters.AddWithValue("id", inviteId.ToString());
        var events = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // jsonb booleans read back as "true"/"false"; show them like C# for the assertion.
            var value = reader.GetString(0);
            events.Add(value.Replace(":true", ":True", StringComparison.Ordinal).Replace(":false", ":False", StringComparison.Ordinal));
        }

        return events;
    }

    private async Task<List<(string Type, Guid? ActorId)>> WaitForActivityAsync(Guid entityId, int count)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT event_type, actor_id FROM activity.activity_events WHERE entity_id = @id ORDER BY event_id", connection);
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

    /// <summary>Waits until some trekky_app session is blocked on a row lock (the accept's UPDATE of the invite).</summary>
    private async Task WaitForLockWaitAsync()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await CountAsync("SELECT count(*) FROM pg_stat_activity WHERE usename = 'trekky_app' AND wait_event_type = 'Lock'") > 0)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("The accept never waited on the invite's row.");
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

    private static async Task<object?> ScalarAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteScalarAsync();
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return await command.ExecuteNonQueryAsync();
    }

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

    private static Task<MeBody> MeAsync(SignedUpUser user) => GetAsync<MeBody>(user, "/api/v1/me");

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

    private sealed record SentInviteBody(InviteBody Invite, string AcceptUrl, bool EmailSent);

    private sealed record InviteBody(
        Guid Id, string Email, string Role, string Status, MeUser? InvitedBy, DateTimeOffset CreatedAt, DateTimeOffset SentAt, DateTimeOffset ExpiresAt);

    private sealed record InviteListBody(List<InviteBody> Items);
}
