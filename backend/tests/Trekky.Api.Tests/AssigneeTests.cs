using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Trekky.Api.Tests;

/// <summary>US-13: the organization's members, and assigning an item to one of them or to nobody, with its event.</summary>
[Collection(ApiCollection.Name)]
public sealed class AssigneeTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task Members_are_the_callers_organization_with_their_role()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory, "Org A");
        using var other = await AuthFlow.SignedUpUserAsync(factory, "Org B");
        var me = await MeAsync(user);

        var members = await GetAsync<JsonElement>(user, "/api/v1/members");

        var only = Assert.Single(members.GetProperty("items").EnumerateArray());
        Assert.Equal(me.Id, only.GetProperty("user").GetProperty("id").GetGuid());
        Assert.Equal(me.DisplayName, only.GetProperty("user").GetProperty("displayName").GetString());
        Assert.Equal("owner", only.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Members_need_an_access_token()
    {
        using var client = factory.CreateBrowserClient();

        var response = await client.GetAsync(new Uri("/api/v1/members", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_item_is_assigned_to_a_member_shown_everywhere_and_recorded()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var me = await MeAsync(user);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "Own me");
        Assert.Equal(JsonValueKind.Null, item.Body.GetProperty("assignee").ValueKind);

        var response = await PatchAsync(user, item.Id, item.ETag, $$"""{ "assigneeId": "{{me.Id}}" }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        AssertAssignee(detail, me);
        Assert.True(detail.GetProperty("updatedAt").GetDateTimeOffset() > item.Body.GetProperty("updatedAt").GetDateTimeOffset());

        var list = await GetAsync<JsonElement>(user, $"/api/v1/projects/{project}/work-items");
        AssertAssignee(list.GetProperty("items")[0], me);
        var board = await GetAsync<JsonElement>(user, $"/api/v1/projects/{project}/board");
        AssertAssignee(board.GetProperty("columns")[0].GetProperty("items")[0], me);

        var assigned = await WaitForEventAsync(user, item.Id, "WorkItemAssigned");
        var payload = assigned.GetProperty("payload");
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("from").ValueKind);
        Assert.Equal(me.Id, payload.GetProperty("to").GetProperty("id").GetGuid());
        Assert.Equal(me.DisplayName, payload.GetProperty("to").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task The_assignee_is_cleared_with_null_and_that_is_recorded_too()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var me = await MeAsync(user);
        var item = await CreateItemAsync(user, await CreateProjectAsync(user, "TRK"), "Hand back");
        var assigned = await PatchAsync(user, item.Id, item.ETag, $$"""{ "assigneeId": "{{me.Id}}" }""");
        await WaitForEventAsync(user, item.Id, "WorkItemAssigned");

        var cleared = await PatchAsync(user, item.Id, assigned.Headers.ETag!.ToString(), """{ "assigneeId": null }""");

        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await cleared.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assignee").ValueKind);
        var events = await WaitForEventsAsync(user, item.Id, "WorkItemAssigned", count: 2);
        Assert.Equal(me.Id, events[0].GetProperty("payload").GetProperty("from").GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, events[0].GetProperty("payload").GetProperty("to").ValueKind);
    }

    [Fact]
    public async Task Someone_who_is_not_a_member_cannot_be_assigned()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory, "Org A");
        using var stranger = await AuthFlow.SignedUpUserAsync(factory, "Org B");
        var outsider = await MeAsync(stranger);
        var item = await CreateItemAsync(user, await CreateProjectAsync(user, "TRK"), "Mine only");

        foreach (var id in new[] { outsider.Id, Guid.CreateVersion7() })
        {
            var response = await PatchAsync(user, item.Id, item.ETag, $$"""{ "assigneeId": "{{id}}" }""");

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Assignee not allowed", problem.RootElement.GetProperty("title").GetString());
        }

        var reread = await GetAsync<JsonElement>(user, $"/api/v1/work-items/{item.Id}");
        Assert.Equal(JsonValueKind.Null, reread.GetProperty("assignee").ValueKind);
    }

    [Fact]
    public async Task Assigning_the_same_person_again_changes_nothing()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var me = await MeAsync(user);
        var item = await CreateItemAsync(user, await CreateProjectAsync(user, "TRK"), "Same");
        var assigned = await PatchAsync(user, item.Id, item.ETag, $$"""{ "assigneeId": "{{me.Id}}" }""");
        var etag = assigned.Headers.ETag!.ToString();

        var again = await PatchAsync(user, item.Id, etag, $$"""{ "assigneeId": "{{me.Id}}" }""");

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(etag, again.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task One_edit_of_title_and_assignee_records_both_events()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var me = await MeAsync(user);
        var item = await CreateItemAsync(user, await CreateProjectAsync(user, "TRK"), "Both");

        var response = await PatchAsync(user, item.Id, item.ETag, $$"""{ "title": "Both at once", "assigneeId": "{{me.Id}}" }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WaitForEventAsync(user, item.Id, "WorkItemAssigned");
        var updated = await WaitForEventAsync(user, item.Id, "WorkItemUpdated");
        Assert.Equal(
            "Both at once",
            updated.GetProperty("payload").GetProperty("changes").GetProperty("title").GetProperty("to").GetString());
        Assert.False(updated.GetProperty("payload").GetProperty("changes").TryGetProperty("assignee", out _));
    }

    [Theory]
    [InlineData("""{ "assigneeId": 42 }""")]
    [InlineData("""{ "assigneeId": "not-a-guid" }""")]
    public async Task An_assignee_that_is_not_an_id_is_a_validation_problem(string json)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var item = await CreateItemAsync(user, await CreateProjectAsync(user, "TRK"), "Typed");

        var response = await PatchAsync(user, item.Id, item.ETag, json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("assigneeId", out _));
    }

    private static void AssertAssignee(JsonElement item, MeUser me)
    {
        var assignee = item.GetProperty("assignee");
        Assert.Equal(me.Id, assignee.GetProperty("id").GetGuid());
        Assert.Equal(me.DisplayName, assignee.GetProperty("displayName").GetString());
    }

    private static async Task<T> GetAsync<T>(SignedUpUser user, string path)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Get, path, user.Tokens.AccessToken);
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<MeUser> MeAsync(SignedUpUser user)
        => (await GetAsync<MeBody>(user, "/api/v1/me")).User;

    private static async Task<Guid> CreateProjectAsync(SignedUpUser user, string key)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Post, "/api/v1/projects", user.Tokens.AccessToken);
        request.Content = JsonContent.Create(new { name = $"Project {key}", key });
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<CreatedItem> CreateItemAsync(SignedUpUser user, Guid projectId, string title)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Post, $"/api/v1/projects/{projectId}/work-items", user.Tokens.AccessToken);
        request.Content = JsonContent.Create(new { type = "task", title });
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new CreatedItem(body.GetProperty("id").GetGuid(), response.Headers.ETag!.ToString(), body);
    }

    private static Task<HttpResponseMessage> PatchAsync(SignedUpUser user, Guid id, string etag, string json)
    {
        var request = AuthFlow.WithBearer(HttpMethod.Patch, $"/api/v1/work-items/{id}", user.Tokens.AccessToken);
        request.Content = new StringContent(json, Encoding.UTF8, "application/merge-patch+json");
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return user.Client.SendAsync(request);
    }

    private static async Task<JsonElement> WaitForEventAsync(SignedUpUser user, Guid itemId, string eventType)
        => (await WaitForEventsAsync(user, itemId, eventType, count: 1))[0];

    /// <summary>The item's events of <paramref name="eventType"/>, newest first, once there are <paramref name="count"/> (the outbox runs every 500 ms).</summary>
    private static async Task<List<JsonElement>> WaitForEventsAsync(SignedUpUser user, Guid itemId, string eventType, int count)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var page = await GetAsync<JsonElement>(user, $"/api/v1/work-items/{itemId}/history");
            var found = page.GetProperty("items").EnumerateArray()
                .Where(e => e.GetProperty("eventType").GetString() == eventType)
                .Select(e => e.Clone())
                .ToList();
            if (found.Count >= count)
            {
                return found;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Fewer than {count} {eventType} events for {itemId} within 10 seconds.");
    }

    private sealed record CreatedItem(Guid Id, string ETag, JsonElement Body);
}
