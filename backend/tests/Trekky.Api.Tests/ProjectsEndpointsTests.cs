using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Trekky.Api.Tests;

/// <summary>US-05: create and list projects, unique immutable keys per organization, ProjectCreated in the activity log.</summary>
[Collection(ApiCollection.Name)]
public sealed class ProjectsEndpointsTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task Creating_a_project_returns_it_with_location_and_etag()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);

        var response = await CreateAsync(user, "  Trekky  ", "TRK");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = (await response.Content.ReadFromJsonAsync<ProjectBody>())!;
        Assert.Equal("TRK", project.Key);
        Assert.Equal("Trekky", project.Name);
        Assert.Equal(0, project.ItemCount);
        Assert.Equal($"/api/v1/projects/{project.Id}", response.Headers.Location?.ToString());
        Assert.NotNull(response.Headers.ETag);

        using var get = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/projects/{project.Id}", user.Tokens.AccessToken);
        var fetched = await user.Client.SendAsync(get);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(response.Headers.ETag, fetched.Headers.ETag);
    }

    [Fact]
    public async Task The_list_is_ordered_by_name_and_pages_with_a_cursor()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        foreach (var (name, key) in new[] { ("Website", "WEB"), ("Api", "API"), ("Mobile", "MOB") })
        {
            Assert.Equal(HttpStatusCode.Created, (await CreateAsync(user, name, key)).StatusCode);
        }

        var first = await ListAsync(user, "?limit=2");
        Assert.Equal(["Api", "Mobile"], first.Items.Select(p => p.Name));
        Assert.NotNull(first.NextCursor);

        var second = await ListAsync(user, $"?limit=2&cursor={Uri.EscapeDataString(first.NextCursor)}");
        Assert.Equal(["Website"], second.Items.Select(p => p.Name));
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task A_key_already_used_in_the_organization_is_rejected_with_409()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        Assert.Equal(HttpStatusCode.Created, (await CreateAsync(user, "Trekky", "TRK")).StatusCode);

        var duplicate = await CreateAsync(user, "Another", "TRK");

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var problem = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        Assert.Equal("A project with this key already exists in your organization.", problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Another_organization_may_use_the_same_key()
    {
        using var userA = await AuthFlow.SignedUpUserAsync(factory, "Org A");
        using var userB = await AuthFlow.SignedUpUserAsync(factory, "Org B");

        Assert.Equal(HttpStatusCode.Created, (await CreateAsync(userA, "Trekky", "TRK")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CreateAsync(userB, "Trekky", "TRK")).StatusCode);
    }

    [Theory]
    [InlineData("", "TRK", "name")]
    [InlineData("Trekky", "trk", "key")]
    [InlineData("Trekky", "T", "key")]
    [InlineData("Trekky", "1TRK", "key")]
    [InlineData("Trekky", "TOOLONGKEY1", "key")]
    public async Task Invalid_input_returns_a_validation_problem_for_the_field(string name, string key, string field)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);

        var response = await CreateAsync(user, name, key);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"No error for {field}");
    }

    [Fact]
    public async Task A_name_longer_than_80_characters_is_rejected()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);

        var response = await CreateAsync(user, new string('a', 81), "TRK");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("?limit=0", "limit")]
    [InlineData("?limit=101", "limit")]
    [InlineData("?cursor=not-a-cursor", "cursor")]
    public async Task Invalid_paging_returns_a_validation_problem(string query, string field)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/projects{query}", user.Tokens.AccessToken);

        var response = await user.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Another_organizations_projects_are_invisible()
    {
        using var userA = await AuthFlow.SignedUpUserAsync(factory, "Org A");
        using var userB = await AuthFlow.SignedUpUserAsync(factory, "Org B");
        var created = await (await CreateAsync(userA, "Secret", "SEC")).Content.ReadFromJsonAsync<ProjectBody>();

        Assert.Empty((await ListAsync(userB, string.Empty)).Items);
        using var get = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/projects/{created!.Id}", userB.Tokens.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await userB.Client.SendAsync(get)).StatusCode);
    }

    [Fact]
    public async Task Projects_need_an_access_token()
    {
        using var client = factory.CreateBrowserClient();

        var response = await client.GetAsync(new Uri("/api/v1/projects", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_project_records_ProjectCreated_in_the_activity_log()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = (await (await CreateAsync(user, "Trekky", "TRK")).Content.ReadFromJsonAsync<ProjectBody>())!;
        var actorId = AuthFlow.DecodeJwtPart(user.Tokens.AccessToken, 1).GetProperty("sub").GetString();

        var activity = await WaitForActivityAsync(project.Id);

        Assert.Equal("ProjectCreated", activity.EventType);
        Assert.Equal("user", activity.ActorType);
        Assert.Equal(actorId, activity.ActorId.ToString());
        Assert.Equal("project", activity.EntityType);
        Assert.Equal("TRK", activity.EntityKey);
        Assert.Equal(project.Id, activity.ProjectId);
        using var payload = JsonDocument.Parse(activity.Payload);
        Assert.Equal("TRK", payload.RootElement.GetProperty("key").GetString());
        Assert.Equal("Trekky", payload.RootElement.GetProperty("name").GetString());

        // The outbox row is marked processed once delivered.
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var processed = new NpgsqlCommand(
            "SELECT processed_at IS NOT NULL FROM projects.outbox WHERE id = @id", connection);
        processed.Parameters.AddWithValue("id", activity.EventId);
        Assert.True((bool)(await processed.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task The_app_role_can_append_to_the_activity_log_but_not_change_it()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = (await (await CreateAsync(user, "Trekky", "TRK")).Content.ReadFromJsonAsync<ProjectBody>())!;
        var activity = await WaitForActivityAsync(project.Id);

        await using var connection = new NpgsqlConnection(factory.AppConnectionString);
        await connection.OpenAsync();
        await using (var tenant = new NpgsqlCommand("SELECT set_config('app.org_id', @org, false)", connection))
        {
            tenant.Parameters.AddWithValue("org", activity.OrgId.ToString());
            await tenant.ExecuteNonQueryAsync();
        }

        foreach (var sql in new[]
        {
            "UPDATE activity.activity_events SET event_type = 'Tampered' WHERE event_id = @id",
            "DELETE FROM activity.activity_events WHERE event_id = @id",
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", activity.EventId);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
    }

    [Fact]
    public async Task A_key_with_a_trailing_newline_is_a_validation_problem_not_a_server_error()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);

        var response = await CreateAsync(user, "Trekky", "TRK\n");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Paging_through_projects_with_the_same_name_returns_each_once()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        foreach (var key in new[] { "SA", "SB", "SC" })
        {
            Assert.Equal(HttpStatusCode.Created, (await CreateAsync(user, "Same", key)).StatusCode);
        }

        var keys = new List<string>();
        string? cursor = null;
        do
        {
            var page = await ListAsync(user, cursor is null ? "?limit=1" : $"?limit=1&cursor={Uri.EscapeDataString(cursor)}");
            keys.AddRange(page.Items.Select(p => p.Key));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal(3, keys.Count);
        Assert.Equal(3, keys.Distinct().Count());
    }

    [Fact]
    public async Task A_poison_outbox_row_does_not_hold_back_later_events_and_only_the_dispatcher_can_see_it()
    {
        // A pending row of another organization whose envelope cannot be delivered; it stays pending.
        var poisonId = Guid.CreateVersion7(DateTimeOffset.UtcNow.AddMinutes(-1));
        var otherOrg = Guid.CreateVersion7();
        await ExecuteAsSuperuserAsync(
            "INSERT INTO projects.outbox (id, org_id, event_type, envelope, created_at) VALUES (@id, @org, 'Broken', '{}', now())",
            ("id", poisonId), ("org", otherOrg));
        try
        {
            using var user = await AuthFlow.SignedUpUserAsync(factory);
            var project = (await (await CreateAsync(user, "Trekky", "TRK")).Content.ReadFromJsonAsync<ProjectBody>())!;
            await WaitForActivityAsync(project.Id);

            await using var connection = new NpgsqlConnection(factory.AppConnectionString);
            await connection.OpenAsync();

            // Without the setting the other tenant's row is invisible.
            await using (var hidden = new NpgsqlCommand("SELECT count(*) FROM projects.outbox WHERE id = @id", connection))
            {
                hidden.Parameters.AddWithValue("id", poisonId);
                Assert.Equal(0L, await hidden.ExecuteScalarAsync());
            }

            // With it, the dispatcher sees the pending row but still cannot change it without that tenant's app.org_id.
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var dispatch = new NpgsqlCommand("SELECT set_config('app.outbox_dispatch', 'on', true)", connection, transaction))
            {
                await dispatch.ExecuteNonQueryAsync();
            }

            await using (var visible = new NpgsqlCommand("SELECT count(*) FROM projects.outbox WHERE id = @id", connection, transaction))
            {
                visible.Parameters.AddWithValue("id", poisonId);
                Assert.Equal(1L, await visible.ExecuteScalarAsync());
            }

            await using var update = new NpgsqlCommand(
                "UPDATE projects.outbox SET processed_at = now() WHERE id = @id", connection, transaction);
            update.Parameters.AddWithValue("id", poisonId);
            Assert.Equal(0, await update.ExecuteNonQueryAsync());
        }
        finally
        {
            await ExecuteAsSuperuserAsync("DELETE FROM projects.outbox WHERE id = @id", ("id", poisonId));
        }
    }

    [Fact]
    public async Task An_event_delivered_twice_is_stored_once()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = (await (await CreateAsync(user, "Trekky", "TRK")).Content.ReadFromJsonAsync<ProjectBody>())!;
        var activity = await WaitForActivityAsync(project.Id);

        // As if the dispatcher crashed between the sink and marking the row: it delivers the event again.
        await ExecuteAsSuperuserAsync("UPDATE projects.outbox SET processed_at = NULL WHERE id = @id", ("id", activity.EventId));
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        for (var attempt = 0; attempt < 50; attempt++)
        {
            await using var processed = new NpgsqlCommand("SELECT processed_at IS NOT NULL FROM projects.outbox WHERE id = @id", connection);
            processed.Parameters.AddWithValue("id", activity.EventId);
            if ((bool)(await processed.ExecuteScalarAsync())!)
            {
                break;
            }

            await Task.Delay(200);
        }

        await using var count = new NpgsqlCommand("SELECT count(*) FROM activity.activity_events WHERE event_id = @id", connection);
        count.Parameters.AddWithValue("id", activity.EventId);
        Assert.Equal(1L, await count.ExecuteScalarAsync());
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

        await command.ExecuteNonQueryAsync();
    }

    private static Task<HttpResponseMessage> CreateAsync(SignedUpUser user, string name, string key)
    {
        var request = AuthFlow.WithBearer(HttpMethod.Post, "/api/v1/projects", user.Tokens.AccessToken);
        request.Content = JsonContent.Create(new { name, key });
        return user.Client.SendAsync(request);
    }

    private static async Task<PageBody> ListAsync(SignedUpUser user, string query)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/projects{query}", user.Tokens.AccessToken);
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PageBody>())!;
    }

    /// <summary>The outbox dispatcher runs every 500 ms; reads as the superuser to see what is really stored.</summary>
    private async Task<ActivityRow> WaitForActivityAsync(Guid entityId)
    {
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        for (var attempt = 0; attempt < 50; attempt++)
        {
            await using var command = new NpgsqlCommand(
                """
                SELECT event_id, org_id, event_type, actor_type, actor_id, entity_type, entity_key, project_id, payload::text
                FROM activity.activity_events WHERE entity_id = @id
                """,
                connection);
            command.Parameters.AddWithValue("id", entityId);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new ActivityRow(
                    reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetGuid(4),
                    reader.GetString(5), reader.GetString(6), reader.GetGuid(7), reader.GetString(8));
            }

            await reader.CloseAsync();
            await Task.Delay(200);
        }

        throw new TimeoutException($"No activity event for {entityId} within 10 seconds.");
    }

    private sealed record ProjectBody(Guid Id, string Key, string Name, int ItemCount, DateTimeOffset CreatedAt);

    private sealed record PageBody(List<ProjectBody> Items, string? NextCursor);

    private sealed record ActivityRow(
        Guid EventId, Guid OrgId, string EventType, string ActorType, Guid ActorId, string EntityType, string EntityKey,
        Guid ProjectId, string Payload);
}
