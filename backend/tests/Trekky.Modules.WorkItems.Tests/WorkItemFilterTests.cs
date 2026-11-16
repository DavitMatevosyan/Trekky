using Trekky.Modules.WorkItems.Application.WorkItems;

namespace Trekky.Modules.WorkItems.Tests;

/// <summary>TRK-11.1: the list filters as read from the query string, and their validation.</summary>
public sealed class WorkItemFilterTests
{
    private readonly WorkItemFilterValidator _validator = new();

    [Fact]
    public void No_values_means_no_filter()
    {
        var filter = WorkItemFilter.From(null, "   ");

        Assert.Empty(filter.Types);
        Assert.Null(filter.Query);
        Assert.True(_validator.Validate(filter).IsValid);
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
