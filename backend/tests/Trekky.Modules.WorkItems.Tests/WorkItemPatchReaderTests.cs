using System.Text.Json.Nodes;
using Trekky.Modules.WorkItems.Application.WorkItems;

namespace Trekky.Modules.WorkItems.Tests;

/// <summary>US-07: a JSON merge patch tells "not sent" from "set to null".</summary>
public sealed class WorkItemPatchReaderTests
{
    [Fact]
    public void Fields_not_sent_stay_unset()
    {
        Assert.True(WorkItemPatchReader.TryRead(JsonNode.Parse("""{ "title": "New" }"""), out var patch, out _));

        Assert.Equal(PatchValue<string?>.Set("New"), patch.Title);
        Assert.False(patch.Type.IsSet);
        Assert.False(patch.DescriptionMd.IsSet);
        Assert.False(patch.ParentId.IsSet);
    }

    [Fact]
    public void Null_clears_the_description_and_the_parent()
    {
        Assert.True(WorkItemPatchReader.TryRead(JsonNode.Parse("""{ "descriptionMd": null, "parentId": null }"""), out var patch, out _));

        Assert.Equal(PatchValue<string?>.Set(null), patch.DescriptionMd);
        Assert.Equal(PatchValue<Guid?>.Set(null), patch.ParentId);
    }

    [Fact]
    public void Reads_a_parent_id()
    {
        var id = Guid.CreateVersion7();

        Assert.True(WorkItemPatchReader.TryRead(JsonNode.Parse($$"""{ "parentId": "{{id}}" }"""), out var patch, out _));

        Assert.Equal(PatchValue<Guid?>.Set(id), patch.ParentId);
    }

    [Theory]
    [InlineData("null", "body")]
    [InlineData("[]", "body")]
    [InlineData("\"text\"", "body")]
    [InlineData("{}", "body")]
    [InlineData("""{ "title": null }""", "title")]
    [InlineData("""{ "type": null }""", "type")]
    [InlineData("""{ "title": 1 }""", "title")]
    [InlineData("""{ "descriptionMd": false }""", "descriptionMd")]
    [InlineData("""{ "parentId": 7 }""", "parentId")]
    [InlineData("""{ "parentId": "nope" }""", "parentId")]
    [InlineData("""{ "status": "done" }""", "status")]
    [InlineData("""{ "Title": "Case matters" }""", "Title")]
    public void Rejects_what_is_not_an_editable_field_of_the_right_kind(string json, string field)
    {
        Assert.False(WorkItemPatchReader.TryRead(JsonNode.Parse(json), out _, out var errors));

        Assert.True(errors.ContainsKey(field), $"No error for {field}: {string.Join(", ", errors.Keys)}");
    }
}
