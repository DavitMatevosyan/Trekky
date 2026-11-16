using System.Net;
using System.Net.Http.Json;

namespace Trekky.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class HealthEndpointTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task Health_returns_ok_without_authentication()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthBody>();
        Assert.Equal("ok", body?.Status);
    }

    private sealed record HealthBody(string Status);
}
