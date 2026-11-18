using System.Net;
using System.Text.Json;

namespace Trekky.Api.Tests;

/// <summary>The generated OpenAPI document the web client is built from (ADR-0010, npm run generate:api).</summary>
[Collection(ApiCollection.Name)]
public sealed class OpenApiDocumentTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task The_document_is_generated_without_errors()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("paths").EnumerateObject().Any());
    }

    [Theory]
    [InlineData("/api/v1/projects/{projectId}/work-items", "get")]
    [InlineData("/api/v1/projects/{projectId}/board", "get")]
    [InlineData("/api/v1/work-items/{workItemId}/move", "post")]
    public async Task The_list_the_board_and_a_board_move_declare_the_same_filters(string path, string method)
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative)));

        var parameters = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("parameters")
            .EnumerateArray()
            .Where(p => p.GetProperty("in").GetString() == "query")
            .Select(p => p.GetProperty("name").GetString())
            .ToList();

        Assert.Superset(
            new HashSet<string?> { "open", "status", "type", "assignee", "parent", "q", "completedEpics" },
            parameters.ToHashSet());
    }
}
