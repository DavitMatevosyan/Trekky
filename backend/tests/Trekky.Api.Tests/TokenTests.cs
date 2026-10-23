using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Trekky.Api.Tests;

/// <summary>ADR-0003 token behaviour: code + PKCE, JWT claims and lifetime, refresh rotation, reuse detection, logout.</summary>
[Collection(ApiCollection.Name)]
public sealed class TokenTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task Code_flow_issues_a_signed_15_minute_jwt_with_user_org_and_role()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory, "Claims Org");
        var (client, email, tokens) = (user.Client, user.Email, user.Tokens);

        Assert.InRange(tokens.ExpiresIn, 890, 900); // 15 minutes, give or take rounding
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));

        var header = AuthFlow.DecodeJwtPart(tokens.AccessToken, 0);
        Assert.Equal("RS256", header.GetProperty("alg").GetString());

        var me = await GetMeAsync(client, tokens.AccessToken);
        var payload = AuthFlow.DecodeJwtPart(tokens.AccessToken, 1);
        Assert.Equal(me.User.Id.ToString(), payload.GetProperty("sub").GetString());
        Assert.Equal(me.Organization.Id.ToString(), payload.GetProperty("org_id").GetString());
        Assert.Equal("owner", payload.GetProperty("role").GetString());
        Assert.Equal(email, payload.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Me_returns_the_current_user_organization_and_role()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory, "Me Org");
        var (client, email, tokens) = (user.Client, user.Email, user.Tokens);

        var me = await GetMeAsync(client, tokens.AccessToken);

        Assert.Equal(email, me.User.Email);
        Assert.Equal("Me Org", me.Organization.Name);
        Assert.Equal("owner", me.Role);
    }

    [Fact]
    public async Task Me_without_a_token_returns_401()
    {
        using var client = factory.CreateBrowserClient();

        var response = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_without_a_session_answers_login_required()
    {
        using var client = factory.CreateBrowserClient();

        var response = await AuthFlow.AuthorizeAsync(client, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", prompt: "none");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal("login_required", query["error"].ToString());
    }

    [Fact]
    public async Task Refresh_rotates_the_refresh_token()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var (client, tokens) = (user.Client, user.Tokens);

        var response = await AuthFlow.RefreshAsync(client, tokens.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(tokens.RefreshToken, refreshed.RefreshToken);
        Assert.NotEqual(tokens.AccessToken, refreshed.AccessToken);
        Assert.Equal("owner", (await GetMeAsync(client, refreshed.AccessToken)).Role);
    }

    [Fact]
    public async Task Reusing_a_refresh_token_revokes_the_whole_chain()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var (client, tokens) = (user.Client, user.Tokens);
        var refreshed = (await (await AuthFlow.RefreshAsync(client, tokens.RefreshToken)).Content.ReadFromJsonAsync<TokenResponse>())!;

        var reuse = await AuthFlow.RefreshAsync(client, tokens.RefreshToken);

        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthFlow.RefreshAsync(client, refreshed.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendMeAsync(client, refreshed.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_tokens_and_ends_the_session()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var (client, tokens) = (user.Client, user.Tokens);

        using var logout = AuthFlow.WithBearer(HttpMethod.Post, "/api/v1/auth/logout", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await AuthFlow.RefreshAsync(client, tokens.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendMeAsync(client, tokens.AccessToken)).StatusCode);

        var authorize = await AuthFlow.AuthorizeAsync(client, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", prompt: "none");
        Assert.Equal("login_required", QueryHelpers.ParseQuery(authorize.Headers.Location!.Query)["error"].ToString());
    }

    [Fact]
    public async Task Logout_without_a_token_returns_401()
    {
        using var client = factory.CreateBrowserClient();

        var response = await client.PostAsync(new Uri("/api/v1/auth/logout", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendMeAsync(HttpClient client, string accessToken)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Get, "/api/v1/me", accessToken);
        return await client.SendAsync(request);
    }

    private static async Task<MeBody> GetMeAsync(HttpClient client, string accessToken)
    {
        var response = await SendMeAsync(client, accessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MeBody>())!;
    }
}
