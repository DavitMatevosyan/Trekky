using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Trekky.Modules.GitHub.Application.GitHub;

namespace Trekky.Modules.GitHub.Infrastructure;

/// <summary>
/// <see cref="IGitHubClient"/> over GitHub's REST API (version 2022-11-28). App endpoints use <see cref="GitHubApp"/>'s
/// JWT, repository endpoints an installation token from <see cref="InstallationTokenCache"/>, and the install check a
/// user token that is used once and dropped. Tokens go only into request headers of this client.
/// </summary>
internal sealed class GitHubClient(HttpClient http, GitHubApp app, InstallationTokenCache tokens) : IGitHubClient
{
    /// <summary>GitHub's largest page.</summary>
    private const int PageSize = 100;

    /// <summary>Pages read when listing a user's installations; a person has far fewer than 1,000.</summary>
    private const int MaxInstallationPages = 10;

    /// <summary>The admin check reads at most this many repositories; a larger installation is refused (narrow it on GitHub).</summary>
    private const int MaxCheckedRepositories = 1000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>The API base with a trailing slash, so relative paths append instead of replacing its last segment.</summary>
    private Uri Api => WithTrailingSlash(app.Options.ApiUrl);

    public async Task<string?> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(WithTrailingSlash(app.Options.WebUrl), "login/oauth/access_token"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = app.Options.ClientId!,
                ["client_secret"] = app.Options.ClientSecret!,
                ["code"] = code,
            }),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // GitHub answers 200 with an "error" field for a bad code.
        using var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);
        var body = await ReadAsync<OAuthTokenResponse>(response, cancellationToken);
        return string.IsNullOrEmpty(body.Error) && !string.IsNullOrEmpty(body.AccessToken) ? body.AccessToken : null;
    }

    public async Task<IReadOnlySet<long>> ListUserInstallationIdsAsync(string userToken, CancellationToken cancellationToken)
    {
        var ids = new HashSet<long>();
        for (var page = 1; page <= MaxInstallationPages; page++)
        {
            using var request = Get($"user/installations?per_page={PageSize}&page={page}", userToken);
            using var response = await SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return ids;
            }

            EnsureSuccess(response);
            var body = await ReadAsync<UserInstallationsResponse>(response, cancellationToken);
            ids.UnionWith((body.Installations ?? []).Select(i => i.Id));
            if (body.Installations is not { Count: PageSize })
            {
                break;
            }
        }

        return ids;
    }

    public async Task<bool> UserAdministersInstallationAsync(string userToken, long installationId, CancellationToken cancellationToken)
    {
        // What the app reaches, counted as the installation itself sees it.
        int installationTotal;
        try
        {
            using var response = await SendAsInstallationAsync(installationId, "installation/repositories?per_page=1&page=1", cancellationToken);
            EnsureSuccess(response);
            installationTotal = (await ReadAsync<RepositoriesResponse>(response, cancellationToken)).TotalCount;
        }
        catch (GitHubInstallationGoneException)
        {
            return false;
        }

        if (installationTotal > MaxCheckedRepositories)
        {
            return false;
        }

        // What the user reaches in it, with their own permission on each repository.
        var administered = 0;
        for (var page = 1; page <= MaxCheckedRepositories / PageSize; page++)
        {
            using var request = Get($"user/installations/{installationId}/repositories?per_page={PageSize}&page={page}", userToken);
            using var response = await SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return false;
            }

            EnsureSuccess(response);
            var body = await ReadAsync<RepositoriesResponse>(response, cancellationToken);
            if (body.TotalCount != installationTotal)
            {
                return false;
            }

            var items = body.Repositories ?? [];
            administered += items.Count(r => r.Permissions?.Admin == true);
            if (items.Count < PageSize)
            {
                break;
            }
        }

        return administered == installationTotal;
    }

    public async Task RevokeUserTokenAsync(string userToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(Api, $"applications/{Uri.EscapeDataString(app.Options.ClientId!)}/token"))
        {
            Content = JsonContent.Create(new { access_token = userToken }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{app.Options.ClientId}:{app.Options.ClientSecret}")));

        // Best effort: the token expires on its own within 8 hours, so a failed revoke does not fail the connection.
        try
        {
            using var response = await SendAsync(request, cancellationToken);
        }
        catch (GitHubUnavailableException)
        {
        }
    }

    public async Task<GitHubInstallationInfo?> GetInstallationAsync(long installationId, CancellationToken cancellationToken)
    {
        using var request = Get($"app/installations/{installationId}", app.CreateJwt());
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response);
        var body = await ReadAsync<InstallationResponse>(response, cancellationToken);
        return new GitHubInstallationInfo(body.Id, body.Account?.Login ?? string.Empty, body.Account?.Type ?? string.Empty);
    }

    public async Task<(IReadOnlyList<GitHubRepositoryInfo> Items, bool Truncated)> ListRepositoriesAsync(
        long installationId, int max, CancellationToken cancellationToken)
    {
        var repositories = new List<GitHubRepositoryInfo>();
        var total = 0;
        for (var page = 1; repositories.Count < max; page++)
        {
            using var response = await SendAsInstallationAsync(
                installationId, $"installation/repositories?per_page={PageSize}&page={page}", cancellationToken);
            EnsureSuccess(response);
            var body = await ReadAsync<RepositoriesResponse>(response, cancellationToken);
            total = body.TotalCount;
            var items = body.Repositories ?? [];
            repositories.AddRange(items.Select(r => new GitHubRepositoryInfo(r.Id, r.FullName, r.DefaultBranch, r.Private, r.HtmlUrl)));
            if (items.Count < PageSize)
            {
                break;
            }
        }

        var listed = repositories
            .Take(max)
            .OrderBy(r => r.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return (listed, total > listed.Count);
    }

    public async Task<bool> BranchExistsAsync(
        long installationId, string fullName, string branch, CancellationToken cancellationToken)
    {
        using var response = await SendAsInstallationAsync(
            installationId, $"repos/{GitHubUrls.EscapeSegments(fullName)}/branches/{GitHubUrls.EscapeSegments(branch)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        EnsureSuccess(response);
        return true;
    }

    public async Task<GitHubPathKind> GetPathKindAsync(
        long installationId, string fullName, string branch, string path, CancellationToken cancellationToken)
    {
        var contents = path.Length == 0 ? "contents" : $"contents/{GitHubUrls.EscapeSegments(path)}";
        using var response = await SendAsInstallationAsync(
            installationId,
            $"repos/{GitHubUrls.EscapeSegments(fullName)}/{contents}?ref={Uri.EscapeDataString(branch)}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return GitHubPathKind.Missing;
        }

        EnsureSuccess(response);

        // A folder is listed as an array of its entries; a file (or symlink, submodule) is one object.
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return body.RootElement.ValueKind == JsonValueKind.Array ? GitHubPathKind.Folder : GitHubPathKind.File;
    }

    public void ForgetInstallation(long installationId) => tokens.Forget(installationId);

    /// <summary>
    /// Sends a GET as the installation. A 401 means the cached token was revoked (the app was uninstalled or the token
    /// rotated): the token is dropped and the request retried once with a fresh one.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsInstallationAsync(
        long installationId, string relativeUrl, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var token = await tokens.GetAsync(installationId, ct => CreateInstallationTokenAsync(installationId, ct), cancellationToken);
            using var request = Get(relativeUrl, token);
            var response = await SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized || attempt == 2)
            {
                return response;
            }

            response.Dispose();
            tokens.Forget(installationId);
        }
    }

    private async Task<(string Token, DateTimeOffset ExpiresAt)> CreateInstallationTokenAsync(
        long installationId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Api, $"app/installations/{installationId}/access_tokens"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", app.CreateJwt());
        using var response = await SendAsync(request, cancellationToken);

        // 404: uninstalled; 403: suspended by the account. Either way the installation cannot be used. A 403 for a rate
        // limit is GitHub being busy, not the installation being gone (EnsureSuccess turns it into a 502).
        var rateLimited = response.Headers.RetryAfter is not null
            || (response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining) && remaining.FirstOrDefault() == "0");
        if (response.StatusCode == HttpStatusCode.NotFound || (response.StatusCode == HttpStatusCode.Forbidden && !rateLimited))
        {
            throw new GitHubInstallationGoneException($"GitHub has no usable installation {installationId}.");
        }

        EnsureSuccess(response);
        var body = await ReadAsync<AccessTokenResponse>(response, cancellationToken);
        if (string.IsNullOrEmpty(body.Token))
        {
            throw new GitHubUnavailableException("GitHub returned an installation token response without a token.");
        }

        return (body.Token, body.ExpiresAt);
    }

    private HttpRequestMessage Get(string relativeUrl, string bearer)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Api, relativeUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return request;
    }

    /// <summary>Sends the request; a network failure or timeout (not the caller's cancellation) becomes <see cref="GitHubUnavailableException"/>.</summary>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new GitHubUnavailableException("GitHub could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubUnavailableException("GitHub did not answer in time.", exception);
        }
    }

    private static Uri WithTrailingSlash(Uri uri)
        => uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            // Only the status: GitHub's body may echo request details and is not needed to act on the failure.
            throw new GitHubUnavailableException($"GitHub answered {(int)response.StatusCode} for {response.RequestMessage?.RequestUri?.AbsolutePath}.");
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
                ?? throw new GitHubUnavailableException("GitHub returned an empty body.");
        }
        catch (JsonException exception)
        {
            throw new GitHubUnavailableException("GitHub returned a body Trekky could not read.", exception);
        }
    }

    private sealed record OAuthTokenResponse(string? AccessToken, string? Error);

    private sealed record UserInstallationsResponse(IReadOnlyList<InstallationResponse>? Installations);

    private sealed record InstallationResponse(long Id, AccountResponse? Account);

    private sealed record AccountResponse(string? Login, string? Type);

    private sealed record AccessTokenResponse(string? Token, DateTimeOffset ExpiresAt);

    private sealed record RepositoriesResponse(int TotalCount, IReadOnlyList<RepositoryResponse>? Repositories);

    /// <summary><see cref="Permissions"/>: the user's own access, sent on the user-token listing only.</summary>
    private sealed record RepositoryResponse(
        long Id, string FullName, string DefaultBranch, bool Private, string HtmlUrl, RepositoryPermissionsResponse? Permissions = null);

    private sealed record RepositoryPermissionsResponse(bool Admin);
}
