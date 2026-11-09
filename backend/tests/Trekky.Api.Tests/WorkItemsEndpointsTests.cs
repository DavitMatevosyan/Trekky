using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Trekky.Api.Tests;

/// <summary>US-06 to US-10: create, list, get, edit and move work items, parent rules, per-project keys, activity events.</summary>
[Collection(ApiCollection.Name)]
public sealed class WorkItemsEndpointsTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task Creating_items_gives_them_the_next_key_in_To_Do_with_location_and_etag()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");

        var first = await CreateAsync(user, project, new { type = "story", title = "  Comments on items  ", descriptionMd = "**Markdown**" });
        var second = await CreateAsync(user, project, new { type = "bug", title = "Key not shown" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var item = (await first.Content.ReadFromJsonAsync<DetailBody>())!;
        Assert.Equal("TRK-1", item.Key);
        Assert.Equal("story", item.Type);
        Assert.Equal("Comments on items", item.Title);
        Assert.Equal("**Markdown**", item.DescriptionMd);
        Assert.Equal("todo", item.Status);
        Assert.Equal(project, item.ProjectId);
        Assert.Null(item.Parent);
        Assert.Empty(item.Children);
        Assert.Equal($"/api/v1/work-items/{item.Id}", first.Headers.Location?.ToString());
        Assert.NotNull(first.Headers.ETag);
        Assert.Equal("TRK-2", (await second.Content.ReadFromJsonAsync<DetailBody>())!.Key);
    }

    [Fact]
    public async Task Keys_are_numbered_per_project()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var trekky = await CreateProjectAsync(user, "TRK");
        var website = await CreateProjectAsync(user, "WEB");

        await CreateItemAsync(user, trekky, "task", "One");
        var web = await CreateItemAsync(user, website, "task", "First on the website");

        Assert.Equal("WEB-1", web.Key);
    }

    [Fact]
    public async Task Concurrent_creates_get_distinct_numbers()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");

        var responses = await Task.WhenAll(Enumerable.Range(1, 8).Select(i => CreateAsync(user, project, new { type = "task", title = $"Item {i}" })));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var keys = await Task.WhenAll(responses.Select(async response => (await response.Content.ReadFromJsonAsync<DetailBody>())!.Key));
        Assert.Equal(Enumerable.Range(1, 8).Select(i => $"TRK-{i}").Order(), keys.Order());
    }

    [Theory]
    [InlineData("story", "epic")]
    [InlineData("task", "epic")]
    [InlineData("task", "story")]
    [InlineData("subtask", "story")]
    [InlineData("subtask", "task")]
    [InlineData("bug", "epic")]
    [InlineData("bug", "story")]
    [InlineData("tech_debt", "epic")]
    public async Task An_allowed_parent_is_accepted_and_shown_on_both_items(string type, string parentType)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var parent = await CreateParentAsync(user, project, parentType);

        var child = await CreateItemAsync(user, project, type, "Child", parent.Id);

        Assert.Equal(parent.Key, child.ParentKey);
        Assert.Equal(parent.Id, child.Parent?.Id);
        var reloaded = await GetAsync(user, $"/api/v1/work-items/{parent.Id}");
        Assert.Equal([child.Key], reloaded.Children.Select(c => c.Key));
    }

    [Theory]
    [InlineData("epic", "epic", new string[0])]
    [InlineData("story", "story", new[] { "epic" })]
    [InlineData("task", "subtask", new[] { "epic", "story" })]
    [InlineData("subtask", "epic", new[] { "story", "task" })]
    [InlineData("bug", "task", new[] { "epic", "story" })]
    [InlineData("tech_debt", "story", new[] { "epic" })]
    public async Task A_parent_the_type_does_not_allow_is_rejected_with_the_allowed_types(
        string type, string parentType, string[] allowed)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var parent = await CreateParentAsync(user, project, parentType);

        var response = await CreateAsync(user, project, new { type, title = "Child", parentId = parent.Id });

        await AssertRuleViolationAsync(response, allowed);
    }

    [Fact]
    public async Task A_SubTask_needs_a_parent()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");

        var response = await CreateAsync(user, project, new { type = "subtask", title = "Orphan" });

        var problem = await AssertRuleViolationAsync(response, ["story", "task"]);
        Assert.Equal("A SubTask needs a parent: Story or Task.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task The_parent_must_be_in_the_same_project()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var trekky = await CreateProjectAsync(user, "TRK");
        var website = await CreateProjectAsync(user, "WEB");
        var epic = await CreateItemAsync(user, website, "epic", "Website epic");

        var otherProject = await CreateAsync(user, trekky, new { type = "story", title = "Story", parentId = epic.Id });
        var unknown = await CreateAsync(user, trekky, new { type = "story", title = "Story", parentId = Guid.CreateVersion7() });

        Assert.Equal(
            "The parent must be a work item in the same project.",
            (await AssertRuleViolationAsync(otherProject, ["epic"])).GetProperty("detail").GetString());
        await AssertRuleViolationAsync(unknown, ["epic"]);
    }

    [Theory]
    [InlineData("""{ "type": "story", "title": "" }""", "title")]
    [InlineData("""{ "type": "story", "title": "   " }""", "title")]
    [InlineData("""{ "type": "story" }""", "title")]
    [InlineData("""{ "type": "feature", "title": "Title" }""", "type")]
    [InlineData("""{ "title": "Title" }""", "type")]
    [InlineData("""{ "type": "story", "title": "Bad \u0000 title" }""", "title")]
    public async Task Invalid_input_returns_a_validation_problem_for_the_field(string body, string field)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        using var request = AuthFlow.WithBearer(HttpMethod.Post, $"/api/v1/projects/{project}/work-items", user.Tokens.AccessToken);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await user.Client.SendAsync(request);

        await AssertFieldErrorAsync(response, field);
    }

    [Fact]
    public async Task A_title_longer_than_200_characters_is_rejected()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");

        var response = await CreateAsync(user, project, new { type = "task", title = new string('a', 201) });

        await AssertFieldErrorAsync(response, "title");
    }

    [Fact]
    public async Task Creating_in_an_unknown_project_is_not_found()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);

        var response = await CreateAsync(user, Guid.CreateVersion7(), new { type = "task", title = "Lost" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_list_shows_newest_updates_first_with_parent_keys_and_pages_with_a_cursor()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var epic = await CreateItemAsync(user, project, "epic", "Epic");
        await CreateItemAsync(user, project, "story", "Story", epic.Id);
        await CreateItemAsync(user, project, "bug", "Bug");

        var first = await ListAsync(user, project, "?limit=2");
        Assert.Equal(["TRK-3", "TRK-2"], first.Items.Select(i => i.Key));
        Assert.Equal(new string?[] { null, "TRK-1" }, first.Items.Select(i => i.ParentKey));
        Assert.NotNull(first.NextCursor);

        var second = await ListAsync(user, project, $"?limit=2&cursor={Uri.EscapeDataString(first.NextCursor)}");
        Assert.Equal(["TRK-1"], second.Items.Select(i => i.Key));
        Assert.Null(second.NextCursor);

        // Editing an item moves it to the top.
        await PatchAsync(user, epic.Id, epic.ETag, """{ "title": "Epic, renamed" }""");
        Assert.Equal("TRK-1", (await ListAsync(user, project, string.Empty)).Items[0].Key);
    }

    [Fact]
    public async Task The_list_filters_by_type_and_by_key_or_title()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var epic = await CreateItemAsync(user, project, "epic", "Dogfood");
        await CreateItemAsync(user, project, "story", "Comments on items", epic.Id);
        await CreateItemAsync(user, project, "task", "Write 100% of the tests");
        for (var i = 4; i <= 10; i++)
        {
            await CreateItemAsync(user, project, "bug", $"Bug {i}");
        }

        // Parent candidates for a Task: Epics and Stories.
        Assert.Equal(["TRK-2", "TRK-1"], (await ListAsync(user, project, "?type=epic&type=story")).Items.Select(i => i.Key));
        // Title contains, case-insensitive.
        Assert.Equal(["TRK-2"], (await ListAsync(user, project, "?q=COMMENTS")).Items.Select(i => i.Key));
        // Key starts with: TRK-1 (the exact key, first) and TRK-10, not TRK-2.
        Assert.Equal(["TRK-1", "TRK-10"], (await ListAsync(user, project, "?q=trk-1")).Items.Select(i => i.Key));
        // LIKE wildcards are literal.
        Assert.Equal(["TRK-3"], (await ListAsync(user, project, "?q=100%25")).Items.Select(i => i.Key));
        Assert.Empty((await ListAsync(user, project, "?q=_")).Items);
        // Filters combine.
        Assert.Equal(["TRK-1"], (await ListAsync(user, project, "?type=epic&q=dog")).Items.Select(i => i.Key));
    }

    [Fact]
    public async Task An_exact_key_comes_first_however_many_keys_start_the_same_way()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        for (var i = 1; i <= 12; i++)
        {
            await CreateItemAsync(user, project, "epic", $"Epic {i}");
        }

        // TRK-1 is the oldest of TRK-1, TRK-10, TRK-11, TRK-12: without the rule it would be cut off at limit 3.
        var first = await ListAsync(user, project, "?q=trk-1&limit=3");
        Assert.Equal(["TRK-1", "TRK-12", "TRK-11"], first.Items.Select(i => i.Key));

        // Later pages leave it out, so paging still returns each item once.
        var second = await ListAsync(user, project, $"?q=trk-1&limit=3&cursor={Uri.EscapeDataString(first.NextCursor!)}");
        Assert.Equal(["TRK-10"], second.Items.Select(i => i.Key));
        Assert.Null(second.NextCursor);

        // With limit 1 it fills the first page alone, and paging still returns every match once.
        var keys = new List<string>();
        string? cursor = null;
        do
        {
            var page = await ListAsync(user, project, cursor is null ? "?q=trk-1&limit=1" : $"?q=trk-1&limit=1&cursor={Uri.EscapeDataString(cursor)}");
            keys.AddRange(page.Items.Select(i => i.Key));
            cursor = page.NextCursor;
        }
        while (cursor is not null);
        Assert.Equal(["TRK-1", "TRK-12", "TRK-11", "TRK-10"], keys);

        // The type filter still applies to it.
        Assert.Empty((await ListAsync(user, project, "?q=TRK-1&type=story")).Items);
    }

    [Fact]
    public async Task Every_query_string_problem_is_reported_at_once()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        using var request = AuthFlow.WithBearer(
            HttpMethod.Get, $"/api/v1/projects/{project}/work-items?type=feature&limit=0", user.Tokens.AccessToken);

        var response = await user.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(["limit", "type"], problem.RootElement.GetProperty("errors").EnumerateObject().Select(e => e.Name).Order());
    }

    [Theory]
    [InlineData("?type=feature", "type")]
    [InlineData("?type=epic&type=Story", "type")]
    public async Task An_unknown_type_filter_is_a_validation_problem(string query, string field)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/projects/{project}/work-items{query}", user.Tokens.AccessToken);

        await AssertFieldErrorAsync(await user.Client.SendAsync(request), field);
    }

    [Fact]
    public async Task A_search_longer_than_200_characters_is_a_validation_problem()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        using var request = AuthFlow.WithBearer(
            HttpMethod.Get, $"/api/v1/projects/{project}/work-items?q={new string('a', 201)}", user.Tokens.AccessToken);

        await AssertFieldErrorAsync(await user.Client.SendAsync(request), "q");
    }

    [Theory]
    [InlineData("?limit=0", "limit")]
    [InlineData("?limit=101", "limit")]
    [InlineData("?cursor=not-a-cursor", "cursor")]
    public async Task Invalid_paging_returns_a_validation_problem(string query, string field)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/projects/{project}/work-items{query}", user.Tokens.AccessToken);

        await AssertFieldErrorAsync(await user.Client.SendAsync(request), field);
    }

    [Fact]
    public async Task An_item_can_be_read_by_its_key()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var created = await CreateItemAsync(user, project, "task", "Find me");

        var byKey = await GetAsync(user, "/api/v1/work-items/by-key/TRK-1");

        Assert.Equal(created.Id, byKey.Id);
        foreach (var missing in new[] { "TRK-2", "WEB-1", "TRK-0", "trk-1", "TRK-99999999999" })
        {
            using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/work-items/by-key/{missing}", user.Tokens.AccessToken);
            Assert.Equal(HttpStatusCode.NotFound, (await user.Client.SendAsync(request)).StatusCode);
        }
    }

    [Fact]
    public async Task Editing_changes_only_the_fields_sent_and_returns_a_new_etag()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var epic = await CreateItemAsync(user, project, "epic", "Epic");
        var item = await CreateItemAsync(user, project, "task", "Draft", descriptionMd: "Old text");

        var response = await PatchAsync(user, item.Id, item.ETag, $$"""{ "title": "Final", "parentId": "{{epic.Id}}" }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<DetailBody>())!;
        Assert.Equal("Final", updated.Title);
        Assert.Equal("task", updated.Type);
        Assert.Equal("Old text", updated.DescriptionMd);
        Assert.Equal("TRK-1", updated.ParentKey);
        Assert.NotEqual(item.ETag, response.Headers.ETag?.ToString());

        // null clears the description and the parent.
        var cleared = await PatchAsync(user, item.Id, response.Headers.ETag!.ToString(), """{ "descriptionMd": null, "parentId": null }""");
        var body = (await cleared.Content.ReadFromJsonAsync<DetailBody>())!;
        Assert.Null(body.DescriptionMd);
        Assert.Null(body.ParentKey);
    }

    [Fact]
    public async Task Editing_with_a_stale_etag_is_rejected_and_keeps_the_newer_change()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Original");
        Assert.Equal(HttpStatusCode.OK, (await PatchAsync(user, item.Id, item.ETag, """{ "title": "First edit" }""")).StatusCode);

        var stale = await PatchAsync(user, item.Id, item.ETag, """{ "title": "Second edit" }""");

        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        using var problem = JsonDocument.Parse(await stale.Content.ReadAsStringAsync());
        Assert.Equal("This item was changed by someone else.", problem.RootElement.GetProperty("detail").GetString());
        var latest = await GetAsync(user, $"/api/v1/work-items/{item.Id}");
        Assert.Equal("First edit", latest.Title);
        Assert.Equal(latest.ETag, stale.Headers.ETag?.ToString());
    }

    [Theory]
    [InlineData("W/{0}")]
    [InlineData("\"0\", {0}")]
    [InlineData("*")]
    public async Task If_match_accepts_a_weak_tag_a_list_or_a_star(string format)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Original");

        var response = await PatchAsync(user, item.Id, string.Format(System.Globalization.CultureInfo.InvariantCulture, format, item.ETag), """{ "title": "Edit" }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Concurrent_edits_of_one_item_apply_one_and_reject_the_other_as_stale()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Original");

        var responses = await Task.WhenAll(
            PatchAsync(user, item.Id, item.ETag, """{ "title": "A" }"""),
            PatchAsync(user, item.Id, item.ETag, """{ "title": "B" }"""));

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.PreconditionFailed],
            responses.Select(r => r.StatusCode).Order());
    }

    [Fact]
    public async Task Editing_without_if_match_is_rejected_with_428()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Original");

        var response = await PatchAsync(user, item.Id, ifMatch: null, """{ "title": "Edit" }""");

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ }""", "body")]
    [InlineData("""[1]""", "body")]
    [InlineData("""{ "title": null }""", "title")]
    [InlineData("""{ "title": 42 }""", "title")]
    [InlineData("""{ "type": "feature" }""", "type")]
    [InlineData("""{ "status": "done" }""", "status")]
    [InlineData("""{ "parentId": "not-a-guid" }""", "parentId")]
    [InlineData("""{ "titel": "Typo" }""", "titel")]
    [InlineData("""{ "title": """, "body")]
    [InlineData("""{ "title": "One", "title": "Two" }""", "body")]
    public async Task An_invalid_edit_returns_a_validation_problem_for_the_field(string body, string field)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Original");

        await AssertFieldErrorAsync(await PatchAsync(user, item.Id, item.ETag, body), field);
    }

    [Fact]
    public async Task An_edit_that_is_not_json_is_rejected_with_415()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Original");

        var response = await PatchAsync(user, item.Id, item.ETag, "title=Edit", "application/x-www-form-urlencoded");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Changing_the_type_rechecks_the_items_own_parent()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var story = await CreateItemAsync(user, project, "story", "Story");
        var task = await CreateItemAsync(user, project, "task", "Task", story.Id);

        var response = await PatchAsync(user, task.Id, task.ETag, """{ "type": "tech_debt" }""");

        await AssertRuleViolationAsync(response, ["epic"]);
    }

    [Fact]
    public async Task Changing_the_type_rechecks_the_children()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var story = await CreateItemAsync(user, project, "story", "Story");
        await CreateItemAsync(user, project, "subtask", "Sub", story.Id);
        await CreateItemAsync(user, project, "task", "Task", story.Id);
        var reloaded = await GetAsync(user, $"/api/v1/work-items/{story.Id}");

        // A Bug may not have a parent of type Bug, a SubTask needs a Story or Task, a Task needs a Story or Epic.
        var response = await PatchAsync(user, story.Id, reloaded.ETag, """{ "type": "bug" }""");

        var problem = await AssertRuleViolationAsync(response, []);
        Assert.Equal(["TRK-2", "TRK-3"], problem.GetProperty("conflictingChildKeys").EnumerateArray().Select(k => k.GetString()));
    }

    [Fact]
    public async Task An_item_cannot_be_its_own_parent()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var story = await CreateItemAsync(user, project, "story", "Story");

        var response = await PatchAsync(user, story.Id, story.ETag, $$"""{ "parentId": "{{story.Id}}" }""");

        Assert.Equal(
            "An item cannot be its own parent.",
            (await AssertRuleViolationAsync(response, ["epic"])).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Creating_and_editing_record_activity_events()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var epic = await CreateItemAsync(user, project, "epic", "Epic");
        var item = await CreateItemAsync(user, project, "story", "Draft", descriptionMd: "Text");
        var actorId = AuthFlow.DecodeJwtPart(user.Tokens.AccessToken, 1).GetProperty("sub").GetString();

        var created = await WaitForActivityAsync(item.Id, "WorkItemCreated");
        Assert.Equal("work_item", created.EntityType);
        Assert.Equal("TRK-2", created.EntityKey);
        Assert.Equal(project, created.ProjectId);
        Assert.Equal(actorId, created.ActorId.ToString());
        using (var payload = JsonDocument.Parse(created.Payload))
        {
            Assert.Equal("story", payload.RootElement.GetProperty("type").GetString());
            Assert.Equal("Draft", payload.RootElement.GetProperty("title").GetString());
            Assert.Equal("todo", payload.RootElement.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("parentId").ValueKind);
        }

        var patch = $$"""{ "title": "Final", "descriptionMd": "New text", "parentId": "{{epic.Id}}", "type": "story" }""";
        Assert.Equal(HttpStatusCode.OK, (await PatchAsync(user, item.Id, item.ETag, patch)).StatusCode);

        var updated = await WaitForActivityAsync(item.Id, "WorkItemUpdated");
        using var changes = JsonDocument.Parse(updated.Payload);
        var fields = changes.RootElement.GetProperty("changes");
        // jsonb keeps object keys in its own order.
        Assert.Equal(["descriptionMd", "parent", "title"], fields.EnumerateObject().Select(f => f.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Draft", fields.GetProperty("title").GetProperty("from").GetString());
        Assert.Equal("Final", fields.GetProperty("title").GetProperty("to").GetString());
        Assert.True(fields.GetProperty("descriptionMd").GetProperty("changed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, fields.GetProperty("parent").GetProperty("from").ValueKind);
        Assert.Equal("TRK-1", fields.GetProperty("parent").GetProperty("to").GetProperty("key").GetString());
    }

    [Fact]
    public async Task An_edit_that_changes_nothing_records_no_event_and_keeps_the_etag()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Same");
        await WaitForActivityAsync(item.Id, "WorkItemCreated");

        var response = await PatchAsync(user, item.Id, item.ETag, """{ "title": " Same " }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(item.ETag, response.Headers.ETag?.ToString());
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand("SELECT count(*) FROM work_items.outbox WHERE envelope->'entity'->>'id' = @id", connection);
        count.Parameters.AddWithValue("id", item.Id.ToString());
        Assert.Equal(1L, await count.ExecuteScalarAsync());
    }

    [Fact]
    public async Task An_item_moves_through_the_workflow_and_always_lists_its_allowed_moves()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Workflow");
        Assert.Equal(["in_progress"], item.AllowedTransitions);

        var inProgress = await MoveAsync(user, item, "in_progress");
        Assert.Equal("in_progress", inProgress.Status);
        Assert.Equal(["todo", "in_review"], inProgress.AllowedTransitions);
        Assert.NotEqual(item.ETag, inProgress.ETag);
        Assert.True(inProgress.UpdatedAt > item.UpdatedAt);

        var inReview = await MoveAsync(user, inProgress, "in_review");
        Assert.Equal(["todo", "in_progress", "done"], inReview.AllowedTransitions);
        var done = await MoveAsync(user, inReview, "done");
        Assert.Equal(["in_progress"], done.AllowedTransitions);
        var reopened = await MoveAsync(user, done, "in_progress");
        var backToStart = await MoveAsync(user, reopened, "todo");

        var reloaded = await GetAsync(user, "/api/v1/work-items/by-key/TRK-1");
        Assert.Equal("todo", reloaded.Status);
        Assert.Equal(["in_progress"], reloaded.AllowedTransitions);
        Assert.Equal(backToStart.ETag, reloaded.ETag);
        Assert.Equal("todo", (await ListAsync(user, project, "")).Items.Single().Status);
    }

    [Theory]
    [InlineData("done")]
    [InlineData("in_review")]
    [InlineData("todo")]
    public async Task A_disallowed_move_is_rejected_with_the_allowed_targets(string to)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Workflow");

        var response = await ChangeStatusAsync(user, item.Id, item.ETag, $$"""{ "to": "{{to}}" }""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(["in_progress"], problem.RootElement.GetProperty("allowedTransitions").EnumerateArray().Select(s => s.GetString()));
        Assert.StartsWith("TRK-1 cannot move from To Do to ", problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal("todo", (await GetAsync(user, $"/api/v1/work-items/{item.Id}")).Status);
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "to": null }""")]
    [InlineData("""{ "to": "blocked" }""")]
    [InlineData("""{ "to": "Done" }""")]
    public async Task A_status_change_needs_a_known_status(string body)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Workflow");

        await AssertFieldErrorAsync(await ChangeStatusAsync(user, item.Id, item.ETag, body), "to");
    }

    [Fact]
    public async Task A_status_change_needs_the_current_etag()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Workflow");
        var moved = await MoveAsync(user, item, "in_progress");

        Assert.Equal((HttpStatusCode)428, (await ChangeStatusAsync(user, item.Id, ifMatch: null, """{ "to": "in_review" }""")).StatusCode);

        var stale = await ChangeStatusAsync(user, item.Id, item.ETag, """{ "to": "in_review" }""");
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(moved.ETag, stale.Headers.ETag?.ToString());
        Assert.Equal("in_progress", (await GetAsync(user, $"/api/v1/work-items/{item.Id}")).Status);
    }

    [Fact]
    public async Task A_status_change_records_a_WorkItemStatusChanged_event()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Workflow");
        var actorId = AuthFlow.DecodeJwtPart(user.Tokens.AccessToken, 1).GetProperty("sub").GetString();

        await MoveAsync(user, item, "in_progress");

        var changed = await WaitForActivityAsync(item.Id, "WorkItemStatusChanged");
        Assert.Equal("TRK-1", changed.EntityKey);
        Assert.Equal(project, changed.ProjectId);
        Assert.Equal(actorId, changed.ActorId.ToString());
        using var payload = JsonDocument.Parse(changed.Payload);
        Assert.Equal("todo", payload.RootElement.GetProperty("from").GetString());
        Assert.Equal("in_progress", payload.RootElement.GetProperty("to").GetString());
    }

    [Fact]
    public async Task The_project_shows_how_many_items_it_has()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        await CreateProjectAsync(user, "WEB");
        await CreateItemAsync(user, project, "task", "One");
        await CreateItemAsync(user, project, "task", "Two");

        using var list = AuthFlow.WithBearer(HttpMethod.Get, "/api/v1/projects", user.Tokens.AccessToken);
        using var page = JsonDocument.Parse(await (await user.Client.SendAsync(list)).Content.ReadAsStringAsync());
        var counts = page.RootElement.GetProperty("items").EnumerateArray()
            .ToDictionary(p => p.GetProperty("key").GetString()!, p => p.GetProperty("itemCount").GetInt32());
        Assert.Equal(new Dictionary<string, int> { ["TRK"] = 2, ["WEB"] = 0 }, counts);

        using var byKey = AuthFlow.WithBearer(HttpMethod.Get, "/api/v1/projects/by-key/TRK", user.Tokens.AccessToken);
        using var single = JsonDocument.Parse(await (await user.Client.SendAsync(byKey)).Content.ReadAsStringAsync());
        Assert.Equal(project, single.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(2, single.RootElement.GetProperty("itemCount").GetInt32());
    }

    [Fact]
    public async Task The_board_has_one_column_per_status_in_workflow_order_with_counts()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var other = await CreateProjectAsync(user, "WEB");
        await CreateItemAsync(user, other, "task", "On another board");
        var epic = await CreateItemAsync(user, project, "epic", "Dogfood");
        var older = await CreateItemAsync(user, project, "story", "Older", epic.Id);
        var newer = await CreateItemAsync(user, project, "bug", "Newer");
        var started = await CreateItemAsync(user, project, "task", "Started");
        await MoveAsync(user, started, "in_progress");

        var board = await GetBoardAsync(user, project, "");

        Assert.Equal(["todo", "in_progress", "in_review", "done"], board.Columns.Select(c => c.Status));
        Assert.Equal([3, 1, 0, 0], board.Columns.Select(c => c.Count));
        Assert.Equal([newer.Key, older.Key, epic.Key], board.Columns[0].Items.Select(i => i.Key));
        Assert.Equal(["TRK-4"], board.Columns[1].Items.Select(i => i.Key));
        Assert.Empty(board.Columns[2].Items);
        var card = board.Columns[0].Items[1];
        Assert.Equal(("story", "Older", "todo", epic.Key), (card.Type, card.Title, card.Status, card.ParentKey));
    }

    [Fact]
    public async Task The_board_shows_the_latest_20_done_cards_with_the_full_count_until_all_are_asked_for()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        await FinishItemsAsync(user, project, 22);

        var latest = (await GetBoardAsync(user, project, "")).Columns[3];
        var all = (await GetBoardAsync(user, project, "?allDone=true")).Columns[3];

        Assert.Equal(22, latest.Count);
        Assert.Equal(Enumerable.Range(3, 20).Reverse().Select(n => $"TRK-{n}"), latest.Items.Select(i => i.Key));
        Assert.Equal(22, all.Count);
        Assert.Equal(Enumerable.Range(1, 22).Reverse().Select(n => $"TRK-{n}"), all.Items.Select(i => i.Key));
    }

    [Fact]
    public async Task The_latest_done_cards_are_the_last_finished_not_the_last_edited()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var done = await FinishItemsAsync(user, project, 21);

        var edited = await PatchAsync(user, done[0].Id, done[0].ETag, """{ "title": "Fixed a typo" }""");
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var afterEdit = (await GetBoardAsync(user, project, "")).Columns[3];
        Assert.DoesNotContain(afterEdit.Items, card => card.Key == "TRK-1");

        var reopened = await MoveAsync(user, done[1], "in_progress");
        var reviewed = await MoveAsync(user, reopened, "in_review");
        await MoveAsync(user, reviewed, "done");
        var afterRefinish = (await GetBoardAsync(user, project, "")).Columns[3];
        Assert.Equal("TRK-2", afterRefinish.Items[0].Key);
    }

    [Fact]
    public async Task A_board_card_carries_the_etag_that_moves_it_until_the_item_changes()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Drag me");

        var card = (await GetBoardAsync(user, project, "")).Columns[0].Items.Single();
        Assert.Equal(item.ETag, card.Etag);

        var moved = await ChangeStatusAsync(user, card.Id, card.Etag, """{ "to": "in_progress" }""");
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        var moveAgain = await ChangeStatusAsync(user, card.Id, card.Etag, """{ "to": "in_review" }""");
        Assert.Equal(HttpStatusCode.PreconditionFailed, moveAgain.StatusCode);
        var fresh = (await GetBoardAsync(user, project, "")).Columns[1].Items.Single();
        Assert.Equal(moved.Headers.ETag!.ToString(), fresh.Etag);
    }

    [Fact]
    public async Task The_board_of_a_project_with_no_items_has_empty_columns()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");

        var board = await GetBoardAsync(user, project, "");

        Assert.Equal(4, board.Columns.Count);
        Assert.All(board.Columns, column =>
        {
            Assert.Equal(0, column.Count);
            Assert.Empty(column.Items);
        });
    }

    [Fact]
    public async Task The_board_needs_an_access_token()
    {
        using var client = factory.CreateBrowserClient();

        var response = await client.GetAsync(new Uri($"/api/v1/projects/{Guid.CreateVersion7()}/board", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Another_organizations_items_are_invisible()
    {
        using var userA = await AuthFlow.SignedUpUserAsync(factory, "Org A");
        using var userB = await AuthFlow.SignedUpUserAsync(factory, "Org B");
        var projectA = await CreateProjectAsync(userA, "SEC");
        var item = await CreateItemAsync(userA, projectA, "epic", "Secret");
        var projectB = await CreateProjectAsync(userB, "MINE");

        foreach (var path in new[]
        {
            $"/api/v1/work-items/{item.Id}",
            "/api/v1/work-items/by-key/SEC-1",
            $"/api/v1/projects/{projectA}/work-items",
            $"/api/v1/projects/{projectA}/board",
            "/api/v1/projects/by-key/SEC",
        })
        {
            using var request = AuthFlow.WithBearer(HttpMethod.Get, path, userB.Tokens.AccessToken);
            Assert.Equal(HttpStatusCode.NotFound, (await userB.Client.SendAsync(request)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await CreateAsync(userB, projectA, new { type = "task", title = "Intruder" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PatchAsync(userB, item.Id, item.ETag, """{ "title": "Hacked" }""")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound, (await ChangeStatusAsync(userB, item.Id, item.ETag, """{ "to": "in_progress" }""")).StatusCode);

        // Another organization's item cannot become a parent either.
        await AssertRuleViolationAsync(await CreateAsync(userB, projectB, new { type = "story", title = "Story", parentId = item.Id }), ["epic"]);
        var unchanged = await GetAsync(userA, $"/api/v1/work-items/{item.Id}");
        Assert.Equal("Secret", unchanged.Title);
        Assert.Equal("todo", unchanged.Status);
    }

    [Fact]
    public async Task Work_items_need_an_access_token()
    {
        using var client = factory.CreateBrowserClient();

        var response = await client.GetAsync(new Uri($"/api/v1/work-items/{Guid.CreateVersion7()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<Guid> CreateProjectAsync(SignedUpUser user, string key)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Post, "/api/v1/projects", user.Tokens.AccessToken);
        request.Content = JsonContent.Create(new { name = $"Project {key}", key });
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> CreateAsync(SignedUpUser user, Guid projectId, object body)
    {
        var request = AuthFlow.WithBearer(HttpMethod.Post, $"/api/v1/projects/{projectId}/work-items", user.Tokens.AccessToken);
        request.Content = JsonContent.Create(body);
        return user.Client.SendAsync(request);
    }

    private static async Task<DetailBody> CreateItemAsync(
        SignedUpUser user, Guid projectId, string type, string title, Guid? parentId = null, string? descriptionMd = null)
    {
        var response = await CreateAsync(user, projectId, new { type, title, parentId, descriptionMd });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DetailBody>())! with { ETag = response.Headers.ETag!.ToString() };
    }

    /// <summary>A parent of <paramref name="type"/>; a SubTask parent needs its own Story first.</summary>
    private static async Task<DetailBody> CreateParentAsync(SignedUpUser user, Guid projectId, string type)
    {
        Guid? grandparent = type == "subtask" ? (await CreateItemAsync(user, projectId, "story", "Story")).Id : null;
        return await CreateItemAsync(user, projectId, type, "Parent", grandparent);
    }

    private static async Task<DetailBody> GetAsync(SignedUpUser user, string path)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Get, path, user.Tokens.AccessToken);
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DetailBody>())! with { ETag = response.Headers.ETag!.ToString() };
    }

    private static async Task<PageBody> ListAsync(SignedUpUser user, Guid projectId, string query)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/projects/{projectId}/work-items{query}", user.Tokens.AccessToken);
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PageBody>())!;
    }

    /// <summary>Creates <paramref name="count"/> tasks and moves each to Done, in key order; returns them with their ETags.</summary>
    private static async Task<List<DetailBody>> FinishItemsAsync(SignedUpUser user, Guid projectId, int count)
    {
        var done = new List<DetailBody>();
        for (var i = 1; i <= count; i++)
        {
            var item = await CreateItemAsync(user, projectId, "task", $"Item {i}");
            var inProgress = await MoveAsync(user, item, "in_progress");
            var inReview = await MoveAsync(user, inProgress, "in_review");
            done.Add(await MoveAsync(user, inReview, "done"));
        }

        return done;
    }

    private static async Task<BoardBody> GetBoardAsync(SignedUpUser user, Guid projectId, string query)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/projects/{projectId}/board{query}", user.Tokens.AccessToken);
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BoardBody>())!;
    }

    private static Task<HttpResponseMessage> PatchAsync(
        SignedUpUser user, Guid id, string? ifMatch, string body, string contentType = "application/merge-patch+json")
    {
        var request = AuthFlow.WithBearer(HttpMethod.Patch, $"/api/v1/work-items/{id}", user.Tokens.AccessToken);
        request.Content = new StringContent(body, Encoding.UTF8);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return user.Client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> ChangeStatusAsync(SignedUpUser user, Guid id, string? ifMatch, string body)
    {
        var request = AuthFlow.WithBearer(HttpMethod.Post, $"/api/v1/work-items/{id}/status", user.Tokens.AccessToken);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return user.Client.SendAsync(request);
    }

    /// <summary>Moves the item and returns it with its new ETag.</summary>
    private static async Task<DetailBody> MoveAsync(SignedUpUser user, DetailBody item, string to)
    {
        var response = await ChangeStatusAsync(user, item.Id, item.ETag, $$"""{ "to": "{{to}}" }""");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DetailBody>())! with { ETag = response.Headers.ETag!.ToString() };
    }

    private static async Task AssertFieldErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"No error for {field}");
    }

    private static async Task<JsonElement> AssertRuleViolationAsync(HttpResponseMessage response, string[] allowedParentTypes)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(allowedParentTypes, problem.RootElement.GetProperty("allowedParentTypes").EnumerateArray().Select(t => t.GetString()));
        return problem.RootElement.Clone();
    }

    /// <summary>The outbox dispatcher runs every 500 ms; reads as the superuser to see what is really stored.</summary>
    private async Task<ActivityRow> WaitForActivityAsync(Guid entityId, string eventType)
    {
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        for (var attempt = 0; attempt < 50; attempt++)
        {
            await using var command = new NpgsqlCommand(
                """
                SELECT actor_id, entity_type, entity_key, project_id, payload::text
                FROM activity.activity_events WHERE entity_id = @id AND event_type = @type
                """,
                connection);
            command.Parameters.AddWithValue("id", entityId);
            command.Parameters.AddWithValue("type", eventType);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new ActivityRow(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetGuid(3), reader.GetString(4));
            }

            await reader.CloseAsync();
            await Task.Delay(200);
        }

        throw new TimeoutException($"No {eventType} event for {entityId} within 10 seconds.");
    }

    private sealed record SummaryBody(Guid Id, string Key, string Type, string Title, string Status, string? ParentKey, DateTimeOffset UpdatedAt);

    private sealed record DetailBody(
        Guid Id,
        string Key,
        string Type,
        string Title,
        string Status,
        string? ParentKey,
        DateTimeOffset UpdatedAt,
        Guid ProjectId,
        string? DescriptionMd,
        SummaryBody? Parent,
        List<SummaryBody> Children,
        List<string> AllowedTransitions,
        DateTimeOffset CreatedAt)
    {
        public string ETag { get; init; } = string.Empty;
    }

    private sealed record PageBody(List<SummaryBody> Items, string? NextCursor);

    private sealed record BoardCardBody(Guid Id, string Key, string Type, string Title, string Status, string? ParentKey, DateTimeOffset UpdatedAt, string Etag);

    private sealed record BoardColumnBody(string Status, int Count, List<BoardCardBody> Items);

    private sealed record BoardBody(List<BoardColumnBody> Columns);

    private sealed record ActivityRow(Guid ActorId, string EntityType, string EntityKey, Guid ProjectId, string Payload);
}
