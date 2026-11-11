using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Trekky.Api.Tests;

/// <summary>US-12 AC1, AC3: Markdown comments on an item, oldest first with author and time, each with a CommentAdded event.</summary>
[Collection(ApiCollection.Name)]
public sealed class CommentsEndpointsTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task A_comment_is_added_with_its_author_and_time_and_can_be_read_back()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var me = await MeAsync(user);
        var item = await CreateItemAsync(user, await CreateProjectAsync(user, "TRK"), "Comments");
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);

        var response = await AddAsync(user, item.Id, "Let's keep comments **flat**.");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var comment = (await response.Content.ReadFromJsonAsync<CommentBody>())!;
        Assert.Equal($"/api/v1/comments/{comment.Id}", response.Headers.Location?.ToString());
        Assert.Equal(item.Id, comment.WorkItemId);
        Assert.Equal(me.Id, comment.AuthorId);
        Assert.Equal(new UserBody(me.Id, me.DisplayName, me.Email), comment.Author);
        Assert.Equal("Let's keep comments **flat**.", comment.BodyMd);
        Assert.False(comment.Edited);
        Assert.InRange(comment.CreatedAt, before, DateTimeOffset.UtcNow.AddSeconds(5));

        Assert.Equal(comment, await GetAsync<CommentBody>(user, $"/api/v1/comments/{comment.Id}"));
    }

    [Fact]
    public async Task Comments_are_listed_oldest_first_and_page_with_a_cursor()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "Busy");
        var other = await CreateItemAsync(user, project, "Quiet");
        for (var i = 1; i <= 5; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await AddAsync(user, item.Id, $"Comment {i}")).StatusCode);
        }

        await AddAsync(user, other.Id, "On another item");

        var first = await GetAsync<PageBody>(user, $"/api/v1/work-items/{item.Id}/comments?limit=2");
        var second = await GetAsync<PageBody>(user, $"/api/v1/work-items/{item.Id}/comments?limit=2&cursor={first.NextCursor}");
        var last = await GetAsync<PageBody>(user, $"/api/v1/work-items/{item.Id}/comments?limit=2&cursor={second.NextCursor}");
        var all = await GetAsync<PageBody>(user, $"/api/v1/work-items/{item.Id}/comments");

        Assert.Equal(
            Enumerable.Range(1, 5).Select(i => $"Comment {i}"),
            first.Items.Concat(second.Items).Concat(last.Items).Select(c => c.BodyMd));
        Assert.Null(last.NextCursor);
        Assert.Equal(5, all.Items.Count);
        Assert.Null(all.NextCursor);
        Assert.Equal(["On another item"], (await GetAsync<PageBody>(user, $"/api/v1/work-items/{other.Id}/comments")).Items.Select(c => c.BodyMd));
    }

    [Fact]
    public async Task An_item_without_comments_has_an_empty_list()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var item = await CreateItemAsync(user, await CreateProjectAsync(user, "TRK"), "Silent");

        var page = await GetAsync<PageBody>(user, $"/api/v1/work-items/{item.Id}/comments");

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task Adding_a_comment_records_CommentAdded_on_the_item_with_an_excerpt()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var me = await MeAsync(user);
        var item = await CreateItemAsync(user, await CreateProjectAsync(user, "TRK"), "Discussed");
        var body = new string('a', 199) + "😀" + new string('b', 50);

        var comment = (await (await AddAsync(user, item.Id, body)).Content.ReadFromJsonAsync<CommentBody>())!;

        var added = await WaitForEventAsync(user, item.Id, "CommentAdded");
        Assert.Equal(me.Id, added.GetProperty("actor").GetProperty("id").GetGuid());
        Assert.Equal("TRK-1", added.GetProperty("entity").GetProperty("key").GetString());
        var payload = added.GetProperty("payload");
        Assert.Equal(comment.Id, payload.GetProperty("commentId").GetGuid());
        // 200 characters would cut the emoji in two; the excerpt stops before it.
        Assert.Equal(new string('a', 199), payload.GetProperty("excerpt").GetString());

        // A comment is not an edit of the item.
        var reread = await GetAsync<JsonElement>(user, $"/api/v1/work-items/{item.Id}");
        Assert.Equal(item.UpdatedAt, reread.GetProperty("updatedAt").GetDateTimeOffset());
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "bodyMd": null }""")]
    [InlineData("""{ "bodyMd": "" }""")]
    [InlineData("""{ "bodyMd": "  \n\t " }""")]
    [InlineData("""{ "bodyMd": "hi\u0000there" }""")]
    public async Task A_comment_needs_something_to_say(string json)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var item = await CreateItemAsync(user, await CreateProjectAsync(user, "TRK"), "Empty");

        await AssertBodyErrorAsync(await PostRawAsync(user, item.Id, json));
        Assert.Empty((await GetAsync<PageBody>(user, $"/api/v1/work-items/{item.Id}/comments")).Items);
    }

    [Fact]
    public async Task A_comment_can_be_20000_characters_but_no_longer()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var item = await CreateItemAsync(user, await CreateProjectAsync(user, "TRK"), "Long");

        Assert.Equal(HttpStatusCode.Created, (await AddAsync(user, item.Id, new string('x', 20_000))).StatusCode);
        await AssertBodyErrorAsync(await AddAsync(user, item.Id, new string('x', 20_001)));
    }

    [Theory]
    [InlineData("?limit=0", "limit")]
    [InlineData("?limit=101", "limit")]
    [InlineData("?cursor=not-a-cursor", "cursor")]
    public async Task Invalid_paging_is_a_validation_problem(string query, string field)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var item = await CreateItemAsync(user, await CreateProjectAsync(user, "TRK"), "Paged");

        using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/work-items/{item.Id}/comments{query}", user.Tokens.AccessToken);
        var response = await user.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Comments_of_an_unknown_item_are_not_found()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var unknown = Guid.CreateVersion7();

        Assert.Equal(HttpStatusCode.NotFound, (await AddAsync(user, unknown, "Hello")).StatusCode);
        using var list = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/work-items/{unknown}/comments", user.Tokens.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.SendAsync(list)).StatusCode);
        using var single = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/comments/{unknown}", user.Tokens.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.SendAsync(single)).StatusCode);
    }

    [Fact]
    public async Task Another_organizations_comments_are_invisible()
    {
        using var userA = await AuthFlow.SignedUpUserAsync(factory, "Org A");
        using var userB = await AuthFlow.SignedUpUserAsync(factory, "Org B");
        var item = await CreateItemAsync(userA, await CreateProjectAsync(userA, "SEC"), "Secret");
        var comment = (await (await AddAsync(userA, item.Id, "Secret plan")).Content.ReadFromJsonAsync<CommentBody>())!;

        Assert.Equal(HttpStatusCode.NotFound, (await AddAsync(userB, item.Id, "Intruder")).StatusCode);
        foreach (var path in new[] { $"/api/v1/work-items/{item.Id}/comments", $"/api/v1/comments/{comment.Id}" })
        {
            using var request = AuthFlow.WithBearer(HttpMethod.Get, path, userB.Tokens.AccessToken);
            Assert.Equal(HttpStatusCode.NotFound, (await userB.Client.SendAsync(request)).StatusCode);
        }

        Assert.Equal(["Secret plan"], (await GetAsync<PageBody>(userA, $"/api/v1/work-items/{item.Id}/comments")).Items.Select(c => c.BodyMd));
    }

    [Fact]
    public async Task Comments_need_an_access_token()
    {
        using var client = factory.CreateBrowserClient();

        var list = await client.GetAsync(new Uri($"/api/v1/work-items/{Guid.CreateVersion7()}/comments", UriKind.Relative));
        var add = await client.PostAsJsonAsync(new Uri($"/api/v1/work-items/{Guid.CreateVersion7()}/comments", UriKind.Relative), new { bodyMd = "Hi" });

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, add.StatusCode);
    }

    private static Task<HttpResponseMessage> AddAsync(SignedUpUser user, Guid itemId, string bodyMd)
    {
        var request = AuthFlow.WithBearer(HttpMethod.Post, $"/api/v1/work-items/{itemId}/comments", user.Tokens.AccessToken);
        request.Content = JsonContent.Create(new { bodyMd });
        return user.Client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PostRawAsync(SignedUpUser user, Guid itemId, string json)
    {
        var request = AuthFlow.WithBearer(HttpMethod.Post, $"/api/v1/work-items/{itemId}/comments", user.Tokens.AccessToken);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return user.Client.SendAsync(request);
    }

    private static async Task AssertBodyErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("bodyMd", out _));
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

    private static async Task<ItemBody> CreateItemAsync(SignedUpUser user, Guid projectId, string title)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Post, $"/api/v1/projects/{projectId}/work-items", user.Tokens.AccessToken);
        request.Content = JsonContent.Create(new { type = "task", title });
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ItemBody>())!;
    }

    /// <summary>The outbox dispatcher runs every 500 ms, so the event reaches the item's history shortly after the change.</summary>
    private static async Task<JsonElement> WaitForEventAsync(SignedUpUser user, Guid itemId, string eventType)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var page = await GetAsync<JsonElement>(user, $"/api/v1/work-items/{itemId}/history");
            foreach (var item in page.GetProperty("items").EnumerateArray())
            {
                if (item.GetProperty("eventType").GetString() == eventType)
                {
                    return item.Clone();
                }
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"No {eventType} event for {itemId} within 10 seconds.");
    }

    private sealed record ItemBody(Guid Id, string Key, DateTimeOffset UpdatedAt);

    private sealed record UserBody(Guid Id, string DisplayName, string? Email);

    private sealed record CommentBody(Guid Id, Guid WorkItemId, Guid AuthorId, UserBody? Author, string BodyMd, DateTimeOffset CreatedAt, bool Edited);

    private sealed record PageBody(List<CommentBody> Items, string? NextCursor);
}
