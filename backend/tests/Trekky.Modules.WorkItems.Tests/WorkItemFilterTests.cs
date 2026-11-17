using Trekky.Modules.WorkItems.Application.WorkItems;

namespace Trekky.Modules.WorkItems.Tests;

/// <summary>TRK-11.1, TREK-52: the list and board filters as read from the query string, and their validation.</summary>
public sealed class WorkItemFilterTests
{
    private readonly WorkItemFilterValidator _validator = new();

    [Fact]
    public void No_values_means_no_filter()
    {
        var filter = WorkItemFilter.From(null, "   ");

        Assert.Empty(filter.Types);
        Assert.Null(filter.Query);
        Assert.Empty(filter.Statuses);
        Assert.Empty(filter.Assignees);
        Assert.Null(filter.Parent);
        Assert.False(filter.OnlyOpen);
        Assert.False(filter.KeepsCompletedEpics);
        Assert.True(_validator.Validate(filter).IsValid);
    }

    [Fact]
    public void Me_is_the_caller_and_none_is_unassigned()
    {
        var caller = Guid.NewGuid();
        var someone = Guid.NewGuid();

        var filter = WorkItemFilter.From(assignees: ["me", someone.ToString(), "none", caller.ToString()], currentUserId: caller);

        Assert.Equal([caller, someone], filter.AssigneeIds);
        Assert.True(filter.IncludesUnassigned);
        Assert.True(_validator.Validate(filter).IsValid);
    }

    [Theory]
    [InlineData("TRK-42")]
    [InlineData(" trk-42 ")]
    public void A_parent_key_is_read_in_any_case(string parent)
    {
        var filter = WorkItemFilter.From(parent: parent);

        Assert.True(filter.TryGetParentKey(out var projectKey, out var number));
        Assert.Equal(("TRK", 42), (projectKey, number));
        Assert.Null(filter.ParentId);
    }

    [Fact]
    public void A_parent_id_is_not_a_key()
    {
        var id = Guid.NewGuid();
        var filter = WorkItemFilter.From(parent: id.ToString());

        Assert.Equal(id, filter.ParentId);
        Assert.False(filter.TryGetParentKey(out _, out _));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    public void Open_is_true_or_false(string open, bool onlyOpen)
    {
        var filter = WorkItemFilter.From(open: open);

        Assert.Equal(onlyOpen, filter.OnlyOpen);
        Assert.True(_validator.Validate(filter).IsValid);
    }

    [Fact]
    public void Completed_epics_are_shown_only_when_asked()
    {
        Assert.False(WorkItemFilter.From(completedEpics: "hide").KeepsCompletedEpics);
        Assert.True(WorkItemFilter.From(completedEpics: "show").KeepsCompletedEpics);
    }

    [Fact]
    public void At_most_50_assignees_can_be_given()
    {
        var fifty = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid().ToString()).ToArray();

        Assert.True(_validator.Validate(WorkItemFilter.From(assignees: fifty)).IsValid);
        Assert.Contains(
            _validator.Validate(WorkItemFilter.From(assignees: [.. fifty, "none"])).Errors, e => e.PropertyName == "assignee");
    }

    [Theory]
    [InlineData("open", "yes")]
    [InlineData("status", "closed")]
    [InlineData("status", "Done")]
    [InlineData("assignee", "Me")]
    [InlineData("assignee", "nobody")]
    [InlineData("parent", "TRK")]
    [InlineData("parent", "TRK-0")]
    [InlineData("completedEpics", "all")]
    public void Rejects_unknown_values_under_their_parameter(string field, string value)
    {
        var filter = field switch
        {
            "open" => WorkItemFilter.From(open: value),
            "status" => WorkItemFilter.From(statuses: [value]),
            "assignee" => WorkItemFilter.From(assignees: [value]),
            "parent" => WorkItemFilter.From(parent: value),
            _ => WorkItemFilter.From(completedEpics: value),
        };

        Assert.Contains(_validator.Validate(filter).Errors, e => e.PropertyName == field);
    }

    [Fact]
    public void Keeps_each_type_once_and_trims_the_search()
    {
        var filter = WorkItemFilter.From(["epic", "story", "epic", ""], "  dog  ");

        Assert.Equal(["epic", "story"], filter.Types);
        Assert.Equal("dog", filter.Query);
    }

    [Theory]
    [InlineData("feature", null, "type")]
    [InlineData("Epic", null, "type")]
    [InlineData("epic", "a\0b", "q")]
    public void Rejects_unknown_types_and_bad_searches(string type, string? query, string field)
        => Assert.Contains(_validator.Validate(WorkItemFilter.From([type], query)).Errors, e => e.PropertyName == field);

    [Fact]
    public void A_search_may_have_200_characters()
    {
        Assert.True(_validator.Validate(WorkItemFilter.From(null, new string('a', 200))).IsValid);
        Assert.False(_validator.Validate(WorkItemFilter.From(null, new string('a', 201))).IsValid);
    }
}
