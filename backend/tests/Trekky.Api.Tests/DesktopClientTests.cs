using System.Net;
using System.Net.Http.Json;
using Npgsql;

namespace Trekky.Api.Tests;

/// <summary>The desktop app's OAuth client (TRK-24, ADR-0017): code + PKCE to a trekky:// deep link, longer-lived refresh tokens.</summary>
[Collection(ApiCollection.Name)]
public sealed class DesktopClientTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task Desktop_client_signs_in_with_code_and_pkce_through_its_deep_link()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory, "Desktop Org");

        var tokens = await AuthFlow.GetTokensAsync(user.Client, AuthFlow.Desktop);

        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
        var payload = AuthFlow.DecodeJwtPart(tokens.AccessToken, 1);
        Assert.Equal("trekky-desktop", payload.GetProperty("client_id").GetString());

        using var me = AuthFlow.WithBearer(HttpMethod.Get, "/api/v1/me", tokens.AccessToken);
        var response = await user.Client.SendAsync(me);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeBody>();
        Assert.Equal(user.Email, body!.User.Email);
    }

    [Fact]
    public async Task Desktop_refresh_token_rotates_and_cannot_be_used_by_the_web_client()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var tokens = await AuthFlow.GetTokensAsync(user.Client, AuthFlow.Desktop);

        var asWeb = await AuthFlow.RefreshAsync(user.Client, tokens.RefreshToken, AuthFlow.Web);
        Assert.Equal(HttpStatusCode.BadRequest, asWeb.StatusCode);

        var refreshed = await AuthFlow.RefreshAsync(user.Client, tokens.RefreshToken, AuthFlow.Desktop);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var rotated = (await refreshed.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(tokens.RefreshToken, rotated.RefreshToken);
    }

    [Fact]
    public async Task Desktop_refresh_tokens_last_90_days_and_web_ones_14()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory); // also holds web tokens
        await AuthFlow.GetTokensAsync(user.Client, AuthFlow.Desktop);
        var subject = AuthFlow.DecodeJwtPart(user.Tokens.AccessToken, 1).GetProperty("sub").GetString()!;

        Assert.Equal(TimeSpan.FromDays(90), await RefreshTokenLifetimeAsync(subject, "trekky-desktop"));
        Assert.Equal(TimeSpan.FromDays(14), await RefreshTokenLifetimeAsync(subject, "trekky-web"));
    }

    [Fact]
    public async Task Each_desktop_refresh_starts_a_new_90_days()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);
        var tokens = await AuthFlow.GetTokensAsync(user.Client, AuthFlow.Desktop);
        var subject = AuthFlow.DecodeJwtPart(tokens.AccessToken, 1).GetProperty("sub").GetString()!;

        var refreshed = await AuthFlow.RefreshAsync(user.Client, tokens.RefreshToken, AuthFlow.Desktop);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(TimeSpan.FromDays(90), await RefreshTokenLifetimeAsync(subject, "trekky-desktop")); // the rotated token
    }

    [Fact]
    public async Task Desktop_client_rejects_a_redirect_uri_it_was_not_registered_with()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory);

        var response = await AuthFlow.AuthorizeAsync(
            user.Client, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", oauthClient: AuthFlow.Desktop, redirectUri: AuthFlow.RedirectUri);

        // An unknown redirect URI is never redirected to: OpenIddict answers with an error page instead.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    private async Task<TimeSpan> RefreshTokenLifetimeAsync(string subject, string clientId)
    {
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT t.expiration_date - t.creation_date
            FROM identity.openiddict_tokens t
            JOIN identity.openiddict_applications a ON a.id = t.application_id
            WHERE t.subject = @subject AND a.client_id = @clientId AND t.type LIKE '%refresh_token' -- OpenIddict stores the token type URN
            ORDER BY t.creation_date DESC
            LIMIT 1
            """, connection);
        command.Parameters.AddWithValue("subject", subject);
        command.Parameters.AddWithValue("clientId", clientId);
        var lifetime = (TimeSpan)(await command.ExecuteScalarAsync())!;
        return TimeSpan.FromSeconds(Math.Round(lifetime.TotalSeconds));
    }
}
