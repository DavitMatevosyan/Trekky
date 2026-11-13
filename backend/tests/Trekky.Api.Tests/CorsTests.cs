using System.Net;

namespace Trekky.Api.Tests;

/// <summary>
/// CORS (TRK-24, ADR-0017): the desktop app's webview (http://tauri.localhost in Development) may call the API and the
/// token endpoint with bearer tokens; other origins get no CORS headers, so browsers block them.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CorsTests(TrekkyApiFactory factory)
{
    private const string DesktopOrigin = "http://tauri.localhost";

    [Fact]
    public async Task Preflight_from_the_desktop_origin_allows_an_edit_with_bearer_and_if_match()
    {
        using var client = factory.CreateBrowserClient();

        var response = await client.SendAsync(Preflight("/api/v1/projects/TREK/items/1", DesktopOrigin, "PATCH", "authorization,content-type,if-match"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(DesktopOrigin, Header(response, "Access-Control-Allow-Origin"));
        Assert.Contains("PATCH", Header(response, "Access-Control-Allow-Methods"), StringComparison.Ordinal);
        var allowed = Header(response, "Access-Control-Allow-Headers")!.ToUpperInvariant();
        Assert.Contains("AUTHORIZATION", allowed, StringComparison.Ordinal);
        Assert.Contains("IF-MATCH", allowed, StringComparison.Ordinal);
        Assert.Null(Header(response, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Preflight_from_another_origin_gets_no_cors_headers()
    {
        using var client = factory.CreateBrowserClient();

        var response = await client.SendAsync(Preflight("/api/v1/me", "https://evil.example", "GET", "authorization"));

        Assert.Null(Header(response, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Api_response_to_the_desktop_origin_allows_it_and_exposes_etag()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        using var request = AuthFlow.WithBearer(HttpMethod.Get, "/api/v1/me", user.Tokens.AccessToken);
        request.Headers.Add("Origin", DesktopOrigin);

        var response = await user.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DesktopOrigin, Header(response, "Access-Control-Allow-Origin"));
        Assert.Contains("ETag", Header(response, "Access-Control-Expose-Headers"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Token_endpoint_answers_the_desktop_origin_with_cors_headers()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);

        var response = await AuthFlow.RedeemCodeAsync(user.Client, AuthFlow.Desktop, request => request.Headers.Add("Origin", DesktopOrigin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DesktopOrigin, Header(response, "Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage Preflight(string path, string origin, string method, string headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, new Uri(path, UriKind.Relative));
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", headers);
        return request;
    }

    private static string? Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
