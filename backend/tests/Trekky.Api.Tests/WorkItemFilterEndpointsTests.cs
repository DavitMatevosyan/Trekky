using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Trekky.Api.Tests;

/// <summary>
/// TREK-52: the filters shared by the list and the board, the completed-epic rule, and their validation. Most tests
/// read one tree of items (see <see cref="CreateTreeAsync"/>).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class WorkItemFilterEndpointsTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task An_epic_in_Done_and_everything_under_it_are_hidden_by_default()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var tree = await CreateTreeAsync(user);

        // The Done task under the open epic stays; the open story, task, subtask, bug and tech debt under the Done epic go.
        await AssertListAsync(user, tree, "", "TRK-1", "TRK-2", "TRK-3", "TRK-4", "TRK-11", "TRK-12");
        await AssertListAsync(user, tree, "?completedEpics=hide", "TRK-1", "TRK-2", "TRK-3", "TRK-4", "TRK-11", "TRK-12");
        await AssertListAsync(user, tree, "?completedEpics=show", Enumerable.Range(1, 12).Select(n => $"TRK-{n}").ToArray());
    }

    [Fact]
    public async Task Open_leaves_out_each_Done_item_on_its_own_also_under_open_epics()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var tree = await CreateTreeAsync(user);

        await AssertListAsync(user, tree, "?open=true", "TRK-1", "TRK-2", "TRK-4", "TRK-12");
        await AssertListAsync(user, tree, "?open=TRUE&completedEpics=show", "TRK-1", "TRK-2", "TRK-4", "TRK-6", "TRK-7", "TRK-8", "TRK-9", "TRK-10", "TRK-12");
        await AssertListAsync(user, tree, "?open=false", "TRK-1", "TRK-2", "TRK-3", "TRK-4", "TRK-11", "TRK-12");
    }

    [Fact]
    public async Task Repeated_values_of_one_filter_are_OR_and_different_filters_are_AND()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var tree = await CreateTreeAsync(user);

        await AssertListAsync(user, tree, "?status=todo&status=in_progress", "TRK-1", "TRK-2", "TRK-4", "TRK-12");
        await AssertListAsync(user, tree, "?status=todo&status=in_progress&completedEpics=show", "TRK-1", "TRK-2", "TRK-4", "TRK-6", "TRK-7", "TRK-8", "TRK-9", "TRK-12");
        await AssertListAsync(user, tree, "?type=task&type=subtask", "TRK-3", "TRK-4", "TRK-11");
        await AssertListAsync(user, tree, "?type=task&status=done", "TRK-3", "TRK-11");
        await AssertListAsync(user, tree, "?q=story", "TRK-2");
        await AssertListAsync(user, tree, "?q=story&completedEpics=show", "TRK-2", "TRK-6");
        await AssertListAsync(user, tree, "?type=bug&completedEpics=show&open=true", "TRK-9", "TRK-12");
    }

    [Fact]
    public async Task The_assignee_filter_takes_people_me_and_none()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var tree = await CreateTreeAsync(user);
        var me = (await GetAsync<MeBody>(user, "/api/v1/me")).User;

        await AssertListAsync(user, tree, "?assignee=me", "TRK-3", "TRK-12");
        await AssertListAsync(user, tree, $"?assignee={me.Id}", "TRK-3", "TRK-12");
        await AssertListAsync(user, tree, "?assignee=none", "TRK-1", "TRK-2", "TRK-4", "TRK-11");
        await AssertListAsync(user, tree, "?assignee=me&assignee=none", "TRK-1", "TRK-2", "TRK-3", "TRK-4", "TRK-11", "TRK-12");
        await AssertListAsync(user, tree, $"?assignee={Guid.NewGuid()}");
        await AssertListAsync(user, tree, "?assignee=me&completedEpics=show", "TRK-3", "TRK-7", "TRK-12");
    }

    [Fact]
    public async Task The_parent_filter_keeps_everything_under_an_item_at_any_depth()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var tree = await CreateTreeAsync(user);

        await AssertListAsync(user, tree, "?parent=TRK-1", "TRK-2", "TRK-3", "TRK-4");
        await AssertListAsync(user, tree, "?parent=trk-1", "TRK-2", "TRK-3", "TRK-4");
        await AssertListAsync(user, tree, $"?parent={tree.Items["TRK-1"]}", "TRK-2", "TRK-3", "TRK-4");
        await AssertListAsync(user, tree, "?parent=TRK-2", "TRK-3", "TRK-4");
        await AssertListAsync(user, tree, "?parent=TRK-1&open=true", "TRK-2", "TRK-4");

        // A Done epic's items stay hidden until completed epics are shown.
        await AssertListAsync(user, tree, "?parent=TRK-5");
        await AssertListAsync(user, tree, "?parent=TRK-5&completedEpics=show", "TRK-6", "TRK-7", "TRK-8", "TRK-9", "TRK-10");

        // A key that is not in this project has nothing under it here.
        await AssertListAsync(user, tree, "?parent=TRK-99");
        var other = await CreateProjectAsync(user, "OTH");
        var otherEpic = await CreateItemAsync(user, other, "epic", "Other epic");
        await CreateItemAsync(user, other, "story", "Other story", otherEpic.Id);
        await AssertListAsync(user, tree, "?parent=OTH-1");
        await AssertListAsync(user, tree, $"?parent={otherEpic.Id}");
    }

    [Fact]
    public async Task The_total_counts_every_match_across_pages()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var tree = await CreateTreeAsync(user);

        var first = await ListAsync(user, tree.ProjectId, "?limit=4");
        Assert.Equal(4, first.Items.Count);
        Assert.Equal(6, first.TotalCount);

        var second = await ListAsync(user, tree.ProjectId, $"?limit=4&cursor={Uri.EscapeDataString(first.NextCursor!)}");
        Assert.Equal(2, second.Items.Count);
        Assert.Equal(6, second.TotalCount);
        Assert.Null(second.NextCursor);

        Assert.Equal(12, (await ListAsync(user, tree.ProjectId, "?completedEpics=show")).TotalCount);
        Assert.Equal(0, (await ListAsync(user, tree.ProjectId, "?parent=TRK-99")).TotalCount);
    }

    [Fact]
    public async Task The_board_takes_the_same_filters_and_counts_only_matches()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var tree = await CreateTreeAsync(user);

        // Default: the Done epic and its cards leave every column, the Done column too; Done cards of the open epic stay.
        AssertBoard(await GetBoardAsync(user, tree.ProjectId, ""),
            todo: ["TRK-1", "TRK-2", "TRK-4"], inProgress: ["TRK-12"], inReview: [], done: ["TRK-3", "TRK-11"]);
        AssertBoard(await GetBoardAsync(user, tree.ProjectId, "?completedEpics=show"),
            todo: ["TRK-1", "TRK-2", "TRK-4", "TRK-6", "TRK-8", "TRK-9"], inProgress: ["TRK-7", "TRK-12"], inReview: ["TRK-10"], done: ["TRK-3", "TRK-5", "TRK-11"]);

        // A status filter keeps every column; the others are empty.
        AssertBoard(await GetBoardAsync(user, tree.ProjectId, "?status=todo"),
            todo: ["TRK-1", "TRK-2", "TRK-4"], inProgress: [], inReview: [], done: []);
        AssertBoard(await GetBoardAsync(user, tree.ProjectId, "?open=true"),
            todo: ["TRK-1", "TRK-2", "TRK-4"], inProgress: ["TRK-12"], inReview: [], done: []);
        AssertBoard(await GetBoardAsync(user, tree.ProjectId, "?parent=TRK-1"),
            todo: ["TRK-2", "TRK-4"], inProgress: [], inReview: [], done: ["TRK-3"]);
        AssertBoard(await GetBoardAsync(user, tree.ProjectId, "?assignee=me&type=task&type=bug"),
            todo: [], inProgress: ["TRK-12"], inReview: [], done: ["TRK-3"]);
    }

    [Fact]
    public async Task The_board_still_shows_the_latest_20_matching_Done_cards_until_all_are_asked_for()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var epic = await CreateItemAsync(user, project, "epic", "Finished epic");
        var finished = new List<string>();
        for (var i = 1; i <= 21; i++)
        {
            var task = await CreateItemAsync(user, project, "task", $"Task {i}", i % 2 == 0 ? epic.Id : null);
            await MoveToAsync(user, task, "done");
            finished.Add(task.Key);
        }

        await MoveToAsync(user, epic, "done");

        // Only the 11 loose tasks match by default; the 10 under the Done epic and the epic itself do not.
        var done = (await GetBoardAsync(user, project, "")).Columns[3];
        Assert.Equal(11, done.Count);
        Assert.Equal(11, done.Items.Count);

        done = (await GetBoardAsync(user, project, "?completedEpics=show")).Columns[3];
        Assert.Equal(22, done.Count);
        Assert.Equal(20, done.Items.Count);
        Assert.Equal(epic.Key, done.Items[0].Key);

        done = (await GetBoardAsync(user, project, "?completedEpics=show&allDone=true")).Columns[3];
        Assert.Equal(22, done.Count);
        Assert.Equal(22, done.Items.Count);
    }

    [Fact]
    public async Task A_card_drops_between_the_cards_the_board_shows_even_with_a_hidden_card_between_them()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        // New cards go on top of To Do, so the column reads C, A, H, B; H is under an epic that is Done.
        var epic = await CreateItemAsync(user, project, "epic", "Finished epic");
        var b = await CreateItemAsync(user, project, "task", "B");
        await CreateItemAsync(user, project, "story", "H, hidden", epic.Id);
        var a = await CreateItemAsync(user, project, "task", "A");
        var c = await CreateItemAsync(user, project, "task", "C");
        await MoveToAsync(user, epic, "done");
        Assert.Equal(["TRK-5", "TRK-4", "TRK-2"], await ToDoKeysAsync(user, project, ""));

        // A board that shows every card sees H between A and B: for that board they are not neighbours.
        Assert.Equal(HttpStatusCode.Conflict, (await BoardMoveAsync(user, c, "todo", a.Id, b.Id, "?completedEpics=show")).StatusCode);

        // The default board does not show H, so A and B are neighbours; C lands directly below A for everyone.
        var moved = await BoardMoveAsync(user, c, "todo", a.Id, b.Id, "");
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(["TRK-4", "TRK-5", "TRK-2"], await ToDoKeysAsync(user, project, ""));
        Assert.Equal(["TRK-4", "TRK-5", "TRK-3", "TRK-2"], await ToDoKeysAsync(user, project, "?completedEpics=show"));

        // To the top of what is shown: directly above the first card shown.
        Assert.Equal(HttpStatusCode.OK, (await BoardMoveAsync(user, b, "todo", null, a.Id, "")).StatusCode);
        Assert.Equal(["TRK-2", "TRK-4", "TRK-5"], await ToDoKeysAsync(user, project, ""));
        Assert.Equal(["TRK-2", "TRK-4", "TRK-5", "TRK-3"], await ToDoKeysAsync(user, project, "?completedEpics=show"));
    }

    [Fact]
    public async Task A_card_moved_on_a_filtered_board_is_placed_among_the_cards_that_filter_shows()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var me = (await GetAsync<MeBody>(user, "/api/v1/me")).User;
        // To Do reads 4, 3, 2, 1; all but 2 are mine.
        var one = await AssignAsync(user, await CreateItemAsync(user, project, "task", "Mine, 1"), me.Id);
        await CreateItemAsync(user, project, "task", "Not mine");
        var three = await AssignAsync(user, await CreateItemAsync(user, project, "task", "Mine, 3"), me.Id);
        var four = await AssignAsync(user, await CreateItemAsync(user, project, "task", "Mine, 4"), me.Id);

        // On the "mine" board 4, 3, 1: drop 4 between 3 and 1.
        var moved = await BoardMoveAsync(user, four, "todo", three.Id, one.Id, "?assignee=me");

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(["TRK-3", "TRK-4", "TRK-1"], await ToDoKeysAsync(user, project, "?assignee=me"));
        Assert.Equal(["TRK-3", "TRK-4", "TRK-2", "TRK-1"], await ToDoKeysAsync(user, project, ""));
    }

    [Fact]
    public async Task A_move_with_an_unknown_filter_value_is_a_validation_problem()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Task");

        var response = await BoardMoveAsync(user, item, "todo", null, null, "?completedEpics=all&open=maybe");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ["completedEpics", "open"],
            problem.RootElement.GetProperty("errors").EnumerateObject().Select(e => e.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_bad_status_in_both_the_body_and_the_query_string_reports_both()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        var item = await CreateItemAsync(user, project, "task", "Task");

        var response = await BoardMoveAsync(user, item, "bogus", null, null, "?status=closed");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var messages = problem.RootElement.GetProperty("errors").GetProperty("status").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m!.StartsWith("Each status filter", StringComparison.Ordinal));
        Assert.Equal(2, messages.Distinct().Count());
    }

    [Theory]
    [InlineData("?open=yes", "open")]
    [InlineData("?open=true&open=false", "open")]
    [InlineData("?status=closed", "status")]
    [InlineData("?status=todo&status=Done", "status")]
    [InlineData("?type=feature", "type")]
    [InlineData("?assignee=someone", "assignee")]
    [InlineData("?assignee=Me", "assignee")]
    [InlineData("?parent=not-a-key", "parent")]
    [InlineData("?parent=TRK-0", "parent")]
    [InlineData("?completedEpics=all", "completedEpics")]
    [InlineData("?completedEpics=Show", "completedEpics")]
    public async Task An_unknown_filter_value_is_a_validation_problem_on_the_list_and_the_board(string query, string field)
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");

        foreach (var path in new[] { "work-items", "board" })
        {
            using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/projects/{project}/{path}{query}", user.Tokens.AccessToken);
            var response = await user.Client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"{path}{query}: no error for {field}");
        }
    }

    [Fact]
    public async Task Every_filter_problem_on_the_board_is_reported_at_once()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var project = await CreateProjectAsync(user, "TRK");
        using var request = AuthFlow.WithBearer(
            HttpMethod.Get, $"/api/v1/projects/{project}/board?open=1&status=x&assignee=x&parent=x&completedEpics=x", user.Tokens.AccessToken);

        var response = await user.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ["assignee", "completedEpics", "open", "parent", "status"],
            problem.RootElement.GetProperty("errors").EnumerateObject().Select(e => e.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_filtered_board_of_an_unknown_project_is_not_found()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        using var request = AuthFlow.WithBearer(HttpMethod.Get, $"/api/v1/projects/{Guid.NewGuid()}/board?open=true", user.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.SendAsync(request)).StatusCode);
    }

    /// <summary>
    /// Project TRK with an open and a Done epic:
    /// <code>
    /// TRK-1  epic     To Do         Open epic
    /// TRK-2    story  To Do         Story in the open epic
    /// TRK-3      task Done   (me)   Finished task in the open epic
    /// TRK-4        subtask To Do    Subtask of the finished task
    /// TRK-5  epic     Done          Finished epic
    /// TRK-6    story  To Do         Story in the finished epic
    /// TRK-7      task In Progress (me) Task in the finished epic
    /// TRK-8        subtask To Do    Subtask in the finished epic
    /// TRK-9    bug    To Do         Bug in the finished epic
    /// TRK-10   tech_debt In Review  Tech debt in the finished epic
    /// TRK-11 task     Done          Loose finished task
    /// TRK-12 bug      In Progress (me) Loose bug
    /// </code>
    /// </summary>
    private static async Task<Tree> CreateTreeAsync(SignedUpUser user)
    {
        var project = await CreateProjectAsync(user, "TRK");
        var me = (await GetAsync<MeBody>(user, "/api/v1/me")).User;

        var openEpic = await CreateItemAsync(user, project, "epic", "Open epic");
        var openStory = await CreateItemAsync(user, project, "story", "Story in the open epic", openEpic.Id);
        var finishedTask = await CreateItemAsync(user, project, "task", "Finished task in the open epic", openStory.Id);
        await CreateItemAsync(user, project, "subtask", "Subtask of the finished task", finishedTask.Id);
        var doneEpic = await CreateItemAsync(user, project, "epic", "Finished epic");
        var story = await CreateItemAsync(user, project, "story", "Story in the finished epic", doneEpic.Id);
        var task = await CreateItemAsync(user, project, "task", "Task in the finished epic", story.Id);
        await CreateItemAsync(user, project, "subtask", "Subtask in the finished epic", task.Id);
        await CreateItemAsync(user, project, "bug", "Bug in the finished epic", doneEpic.Id);
        var techDebt = await CreateItemAsync(user, project, "tech_debt", "Tech debt in the finished epic", doneEpic.Id);
        var looseTask = await CreateItemAsync(user, project, "task", "Loose finished task");
        var looseBug = await CreateItemAsync(user, project, "bug", "Loose bug");

        await MoveToAsync(user, await AssignAsync(user, finishedTask, me.Id), "done");
        await MoveToAsync(user, doneEpic, "done");
        await MoveToAsync(user, await AssignAsync(user, task, me.Id), "in_progress");
        await MoveToAsync(user, techDebt, "in_review");
        await MoveToAsync(user, looseTask, "done");
        await MoveToAsync(user, await AssignAsync(user, looseBug, me.Id), "in_progress");

        var all = await ListAsync(user, project, "?completedEpics=show&limit=100");
        return new Tree(project, all.Items.ToDictionary(i => i.Key, i => i.Id));
    }

    private static async Task AssertListAsync(SignedUpUser user, Tree tree, string query, params string[] expectedKeys)
    {
        var page = await ListAsync(user, tree.ProjectId, query.Length == 0 ? "?limit=100" : $"{query}&limit=100");
        Assert.Equal(expectedKeys.Order(StringComparer.Ordinal), page.Items.Select(i => i.Key).Order(StringComparer.Ordinal));
        Assert.Equal(expectedKeys.Length, page.TotalCount);
    }

    private static void AssertBoard(BoardBody board, string[] todo, string[] inProgress, string[] inReview, string[] done)
    {
        Assert.Equal(["todo", "in_progress", "in_review", "done"], board.Columns.Select(c => c.Status));
        string[][] expected = [todo, inProgress, inReview, done];
        for (var i = 0; i < expected.Length; i++)
        {
            var column = board.Columns[i];
            Assert.Equal(expected[i].Order(StringComparer.Ordinal), column.Items.Select(c => c.Key).Order(StringComparer.Ordinal));
            Assert.Equal(expected[i].Length, column.Count);
        }
    }

    private static async Task<T> GetAsync<T>(SignedUpUser user, string path)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Get, path, user.Tokens.AccessToken);
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static Task<PageBody> ListAsync(SignedUpUser user, Guid projectId, string query)
        => GetAsync<PageBody>(user, $"/api/v1/projects/{projectId}/work-items{query}");

    private static Task<BoardBody> GetBoardAsync(SignedUpUser user, Guid projectId, string query)
        => GetAsync<BoardBody>(user, $"/api/v1/projects/{projectId}/board{query}");

    private static async Task<Guid> CreateProjectAsync(SignedUpUser user, string key)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Post, "/api/v1/projects", user.Tokens.AccessToken);
        request.Content = JsonContent.Create(new { name = $"Project {key}", key });
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Item> CreateItemAsync(SignedUpUser user, Guid projectId, string type, string title, Guid? parentId = null)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Post, $"/api/v1/projects/{projectId}/work-items", user.Tokens.AccessToken);
        request.Content = JsonContent.Create(new { type, title, parentId });
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ItemAsync(response);
    }

    private static async Task<Item> AssignAsync(SignedUpUser user, Item item, Guid assigneeId)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Patch, $"/api/v1/work-items/{item.Id}", user.Tokens.AccessToken);
        request.Content = new StringContent($$"""{ "assigneeId": "{{assigneeId}}" }""", Encoding.UTF8, "application/merge-patch+json");
        request.Headers.TryAddWithoutValidation("If-Match", item.ETag);
        var response = await user.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ItemAsync(response);
    }

    /// <summary>Walks the item from To Do through the workflow to <paramref name="status"/>.</summary>
    private static async Task<Item> MoveToAsync(SignedUpUser user, Item item, string status)
    {
        string[] path = status switch
        {
            "in_progress" => ["in_progress"],
            "in_review" => ["in_progress", "in_review"],
            "done" => ["in_progress", "in_review", "done"],
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Not a status after To Do."),
        };

        foreach (var to in path)
        {
            using var request = AuthFlow.WithBearer(HttpMethod.Post, $"/api/v1/work-items/{item.Id}/status", user.Tokens.AccessToken);
            request.Content = JsonContent.Create(new { to });
            request.Headers.TryAddWithoutValidation("If-Match", item.ETag);
            var response = await user.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            item = await ItemAsync(response);
        }

        return item;
    }

    private static async Task<HttpResponseMessage> BoardMoveAsync(SignedUpUser user, Item item, string status, Guid? afterId, Guid? beforeId, string query)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Post, $"/api/v1/work-items/{item.Id}/move{query}", user.Tokens.AccessToken);
        request.Content = JsonContent.Create(new { status, afterId, beforeId });
        request.Headers.TryAddWithoutValidation("If-Match", item.ETag);
        return await user.Client.SendAsync(request);
    }

    /// <summary>The To Do column's keys in board order.</summary>
    private static async Task<List<string>> ToDoKeysAsync(SignedUpUser user, Guid projectId, string query)
        => (await GetBoardAsync(user, projectId, query)).Columns[0].Items.Select(c => c.Key).ToList();

    private static async Task<Item> ItemAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Item(body.GetProperty("id").GetGuid(), body.GetProperty("key").GetString()!, response.Headers.ETag!.ToString());
    }

    private sealed record Tree(Guid ProjectId, Dictionary<string, Guid> Items);

    private sealed record Item(Guid Id, string Key, string ETag);

    private sealed record SummaryBody(Guid Id, string Key, string Status);

    private sealed record PageBody(List<SummaryBody> Items, string? NextCursor, int TotalCount);

    private sealed record BoardCardBody(Guid Id, string Key);

    private sealed record BoardColumnBody(string Status, int Count, List<BoardCardBody> Items);

    private sealed record BoardBody(List<BoardColumnBody> Columns);
}
