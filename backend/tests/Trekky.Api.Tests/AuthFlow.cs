using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;

namespace Trekky.Api.Tests;

/// <summary>What the React app will do (ADR-0003): sign up or log in for the session cookie, then authorization code + PKCE.</summary>
internal static class AuthFlow
{
    public const string ClientId = "trekky-web";
    public const string RedirectUri = "http://localhost:5173/auth/callback";
    public const string Password = "correct horse battery staple";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    public static Task<HttpResponseMessage> SignUpAsync(HttpClient client, string email, string organizationName = "Acme", string password = Password)
        => client.PostAsJsonAsync(new Uri("/api/v1/auth/sign-up", UriKind.Relative), new { email, password, organizationName });

    public static Task<HttpResponseMessage> LogInAsync(HttpClient client, string email, string password = Password)
        => client.PostAsJsonAsync(new Uri("/api/v1/auth/login", UriKind.Relative), new { email, password });

    /// <summary>Calls /connect/authorize with the session cookie; returns the redirect.</summary>
    public static async Task<HttpResponseMessage> AuthorizeAsync(HttpClient client, string codeChallenge, string? prompt = null)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = RedirectUri,
            ["scope"] = "openid email profile offline_access",
            ["state"] = "xyz",
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["prompt"] = prompt,
        };
        return await client.GetAsync(new Uri(QueryHelpers.AddQueryString("/connect/authorize", query), UriKind.Relative));
    }

    /// <summary>Runs authorization code + PKCE for the logged-in client and returns the tokens.</summary>
    public static async Task<TokenResponse> GetTokensAsync(HttpClient client)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var authorize = await AuthorizeAsync(client, challenge);
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var redirect = authorize.Headers.Location!;
        Assert.StartsWith(RedirectUri, redirect.ToString(), StringComparison.Ordinal);
        var code = QueryHelpers.ParseQuery(redirect.Query)["code"].ToString();
        Assert.False(string.IsNullOrEmpty(code), $"No code in {redirect}");

        var response = await client.PostAsync(new Uri("/connect/token", UriKind.Relative), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = verifier,
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    public static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken)
        => client.PostAsync(new Uri("/connect/token", UriKind.Relative), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = ClientId,
            ["refresh_token"] = refreshToken,
        }));

    public static HttpRequestMessage WithBearer(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new("Bearer", accessToken);
        return request;
    }

    /// <summary>A browser-like client that has signed up a new user and holds their tokens.</summary>
    public static async Task<SignedUpUser> SignedUpUserAsync(TrekkyApiFactory factory, string organizationName = "Acme")
    {
        var client = factory.CreateBrowserClient();
        var email = NewEmail();
        var signUp = await SignUpAsync(client, email, organizationName);
        Assert.Equal(HttpStatusCode.Created, signUp.StatusCode);
        return new SignedUpUser(client, email, await GetTokensAsync(client));
    }

    /// <summary>Decodes a JWT part (header or payload) without validating it.</summary>
    public static JsonElement DecodeJwtPart(string jwt, int part)
    {
        var segment = jwt.Split('.')[part].Replace('-', '+').Replace('_', '/');
        segment = segment.PadRight(segment.Length + ((4 - (segment.Length % 4)) % 4), '=');
        return JsonDocument.Parse(Convert.FromBase64String(segment)).RootElement.Clone();
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

internal sealed record SignedUpUser(HttpClient Client, string Email, TokenResponse Tokens) : IDisposable
{
    public void Dispose() => Client.Dispose();
}

internal sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

internal sealed record MeBody(MeUser User, MeOrganization Organization, string Role);

internal sealed record MeUser(Guid Id, string DisplayName, string? Email);

internal sealed record MeOrganization(Guid Id, string Name);

internal sealed record ProblemBody(string? Title, string? Detail, int? Status, Dictionary<string, string[]>? Errors);
