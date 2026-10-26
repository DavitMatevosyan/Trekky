using System.Net;

namespace Trekky.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class UnknownRouteTests(TrekkyApiFactory factory)
{
    [Theory]
    [InlineData("GET", "/api/v1/does-not-exist")]
    [InlineData("GET", "/health")]
    [InlineData("GET", "/favicon.ico")]
    [InlineData("POST", "/api/v1/does-not-exist")]
    public async Task Unknown_url_is_not_found_as_problem_details_not_unauthorized(string method, string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Known_endpoint_still_requires_a_token()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_method_on_a_known_route_is_method_not_allowed()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        using var request = AuthFlow.WithBearer(HttpMethod.Get, "/api/v1/auth/logout", user.Tokens.AccessToken);

        var response = await user.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Discovery_document_is_still_served()
    {
        using var client = factory.CreateBrowserClient();

        var response = await client.GetAsync(new Uri("/.well-known/openid-configuration", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
