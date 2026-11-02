using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.Modules.WorkItems.Application.WorkItems;

namespace Trekky.Modules.WorkItems.Tests;

/// <summary>US-06 AC1 field rules, for create and edit.</summary>
public sealed class WorkItemValidatorsTests
{
    private readonly CreateWorkItemRequestValidator _create = new();
    private readonly WorkItemPatchValidator _patch = new();

    [Theory]
    [InlineData("epic")]
    [InlineData("story")]
    [InlineData("task")]
    [InlineData("subtask")]
    [InlineData("bug")]
    [InlineData("tech_debt")]
    public void Accepts_every_type(string type)
        => Assert.True(_create.Validate(new CreateWorkItemRequest(type, "Title")).IsValid);

    [Theory]
    [InlineData(null, "Title", "type")]
    [InlineData("Story", "Title", "type")]
    [InlineData("feature", "Title", "type")]
    [InlineData("story", null, "title")]
    [InlineData("story", "", "title")]
    [InlineData("story", "  ", "title")]
    [InlineData("story", "a\0b", "title")]
    public void Rejects_a_bad_create(string? type, string? title, string field)
        => Assert.Contains(_create.Validate(new CreateWorkItemRequest(type, title)).Errors, e => e.PropertyName == field);

    [Fact]
    public void A_title_may_have_200_characters_after_trimming()
    {
        Assert.True(_create.Validate(new CreateWorkItemRequest("task", $"  {new string('a', 200)}  ")).IsValid);
        Assert.False(_create.Validate(new CreateWorkItemRequest("task", new string('a', 201))).IsValid);
    }

    [Fact]
    public void A_description_may_have_50000_characters()
    {
        Assert.True(_create.Validate(new CreateWorkItemRequest("task", "T", new string('a', 50_000))).IsValid);
        Assert.Contains(
            _create.Validate(new CreateWorkItemRequest("task", "T", new string('a', 50_001))).Errors,
            e => e.PropertyName == "descriptionMd");
    }

    [Fact]
    public void An_edit_checks_only_the_fields_it_sends()
    {
        var titleOnly = new WorkItemPatch(PatchValue<string?>.Unset, PatchValue<string?>.Set("New"), PatchValue<string?>.Unset, PatchValue<Guid?>.Unset);
        var badType = titleOnly with { Type = PatchValue<string?>.Set("feature") };
        var blankTitle = titleOnly with { Title = PatchValue<string?>.Set(" ") };

        Assert.True(_patch.Validate(titleOnly).IsValid);
        Assert.Contains(_patch.Validate(badType).Errors, e => e.PropertyName == "type");
        Assert.Contains(_patch.Validate(blankTitle).Errors, e => e.PropertyName == "title");
    }
}
