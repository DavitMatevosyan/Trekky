using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Trekky.Api.Tests;

/// <summary>US-09: an item's history from the activity log, newest first, with actor names; createdBy on the item.</summary>
[Collection(ApiCollection.Name)]
public sealed class WorkItemHistoryTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task History_lists_every_change_newest_first_with_the_actors_name()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var me = await MeAsync(user);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "Comments");
        var etag = await EditAsync(user, item.Id, item.ETag, """{ "title": "Comments on items" }""");
        await MoveAsync(user, item.Id, etag, "in_progress");

        var page = await WaitForHistoryAsync(user, item.Id, count: 3);

        Assert.Equal(["WorkItemStatusChanged", "WorkItemUpdated", "WorkItemCreated"], page.Items.Select(e => e.EventType));
        Assert.Null(page.NextCursor);
        Assert.All(page.Items, e =>
        {
            Assert.Equal(new ActorBody("user", me.Id, me.DisplayName), e.Actor);
            Assert.Equal(new EntityBody("work_item", item.Id, "TRK-1"), e.Entity);
            Assert.Equal(1, e.SchemaVersion);
        });
        Assert.True(page.Items[0].OccurredAt >= page.Items[2].OccurredAt);
        Assert.Equal("in_progress", page.Items[0].Payload.GetProperty("to").GetString());
        Assert.Equal(
            "Comments on items",
            page.Items[1].Payload.GetProperty("changes").GetProperty("title").GetProperty("to").GetString());
        Assert.Equal("Comments", page.Items[2].Payload.GetProperty("title").GetString());
    }

    [Fact]
    public async Task History_pages_with_a_cursor_without_gaps_or_repeats()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "Title 0");
        var etag = item.ETag;
        for (var i = 1; i <= 4; i++)
        {
            etag = await EditAsync(user, item.Id, etag, $$"""{ "title": "Title {{i}}" }""");
        }

        var all = await WaitForHistoryAsync(user, item.Id, count: 5);

        var first = await HistoryAsync(user, item.Id, "?limit=2");
        var second = await HistoryAsync(user, item.Id, $"?limit=2&cursor={first.NextCursor}");
        var last = await HistoryAsync(user, item.Id, $"?limit=2&cursor={second.NextCursor}");

        Assert.Equal(2, first.Items.Count);
        Assert.Equal(2, second.Items.Count);
        Assert.Single(last.Items);
        Assert.Null(last.NextCursor);
        Assert.Equal(
            all.Items.Select(e => e.EventId),
            first.Items.Concat(second.Items).Concat(last.Items).Select(e => e.EventId));
    }

    [Fact]
    public async Task The_item_shows_who_created_it()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var me = await MeAsync(user);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "Mine");

        using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/work-items/{item.Id}", user.Tokens.AccessToken);
        using var body = JsonDocument.Parse(await (await user.Client.SendAsync(request)).Content.ReadAsStringAsync());

        var createdBy = body.RootElement.GetProperty("createdBy");
        Assert.Equal(me.Id, createdBy.GetProperty("id").GetGuid());
        Assert.Equal(me.DisplayName, createdBy.GetProperty("displayName").GetString());
        Assert.Equal(user.Email, createdBy.GetProperty("email").GetString());
        Assert.Equal(me.Id, item.CreatedBy?.Id);
    }

    [Fact]
    public async Task An_unknown_item_has_no_history()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);

        using var request = AuthFlow.WithBearer(
            HttpMethod.Get, $"/api/v1/work-items/{Guid.CreateVersion7()}/history", user.Tokens.AccessToken);
        var response = await user.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Another_organizations_history_is_invisible()
    {
        using var userA = await AuthFlow.SignedUpUserAsync(factory, "Org A");
        using var userB = await AuthFlow.SignedUpUserAsync(factory, "Org B");
        var project = await CreateProjectAsync(userA, "SEC");
        var item = await CreateItemAsync(userA, project, "Secret");
        await WaitForHistoryAsync(userA, item.Id, count: 1);

        using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/work-items/{item.Id}/history", userB.Tokens.AccessToken);
        var response = await userB.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("?limit=0", "limit")]
    [InlineData("?limit=101", "limit")]
    [InlineData("?cursor=not-a-cursor", "cursor")]
    public async Task Invalid_paging_returns_a_validation_problem(string query, string field)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "Item");

        using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/work-items/{item.Id}/history{query}", user.Tokens.AccessToken);
        var response = await user.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"No error for {field}");
    }

    [Fact]
    public async Task A_limit_that_is_not_a_number_is_a_bad_request_problem()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "Item");

        using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/work-items/{item.Id}/history?limit=abc", user.Tokens.AccessToken);
        var response = await user.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task History_needs_an_access_token()
    {
        using var client = factory.CreateBrowserClient();

        var response = await client.GetAsync(new Uri($"/api/v1/work-items/{Guid.CreateVersion7()}/history", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<MeUser> MeAsync(SignedUpUser user)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Get, "/api/v1/me", user.Tokens.AccessToken);
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MeBody>())!.User;
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

    private static async Task<ItemBody> CreateItemAsync(SignedUpUser user, Guid projectId, string title)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Post, $"/api/v1/projects/{projectId}/work-items", user.Tokens.AccessToken);
        request.Content = JsonContent.Create(new { type = "task", title });
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ItemBody>())! with { ETag = response.Headers.ETag!.ToString() };
    }

    /// <summary>Edits the item and returns its new ETag.</summary>
    private static async Task<string> EditAsync(SignedUpUser user, Guid id, string etag, string body)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Patch, $"/api/v1/work-items/{id}", user.Tokens.AccessToken);
        request.Content = new StringContent(body, Encoding.UTF8, "application/merge-patch+json");
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.ETag!.ToString();
    }

    private static async Task MoveAsync(SignedUpUser user, Guid id, string etag, string to)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Post, $"/api/v1/work-items/{id}/status", user.Tokens.AccessToken);
        request.Content = JsonContent.Create(new { to });
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        Assert.Equal(HttpStatusCode.OK, (await user.Client.SendAsync(request)).StatusCode);
    }

    private static async Task<HistoryPageBody> HistoryAsync(SignedUpUser user, Guid id, string query = "")
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/work-items/{id}/history{query}", user.Tokens.AccessToken);
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HistoryPageBody>())!;
    }

    /// <summary>The outbox dispatcher runs every 500 ms, so events reach the history shortly after the change.</summary>
    private static async Task<HistoryPageBody> WaitForHistoryAsync(SignedUpUser user, Guid id, int count)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var page = await HistoryAsync(user, id);
            if (page.Items.Count >= count)
            {
                return page;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Fewer than {count} history events for {id} within 10 seconds.");
    }

    private sealed record ItemBody(Guid Id, string Key, UserRefBody? CreatedBy)
    {
        public string ETag { get; init; } = string.Empty;
    }

    private sealed record UserRefBody(Guid Id, string DisplayName, string? Email);

    private sealed record HistoryPageBody(List<HistoryEventBody> Items, string? NextCursor);

    private sealed record HistoryEventBody(
        Guid EventId, string EventType, int SchemaVersion, DateTimeOffset OccurredAt, ActorBody Actor, EntityBody Entity, JsonElement Payload);

    private sealed record ActorBody(string Type, Guid? Id, string? DisplayName);

    private sealed record EntityBody(string Type, Guid Id, string? Key);
}
