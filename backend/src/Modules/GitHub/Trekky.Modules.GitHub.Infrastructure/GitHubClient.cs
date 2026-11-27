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

    /// <summary>Asks GitHub for a file's or blob's bytes instead of JSON with base64.</summary>
    private const string RawMediaType = "application/vnd.github.raw+json";

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

    public async Task<GitHubCommitInfo?> GetBranchHeadAsync(
        long installationId, string fullName, string branch, CancellationToken cancellationToken)
    {
        // The branch endpoint: its commit comes without the file patches /commits/{ref} would send.
        using var response = await SendAsInstallationAsync(
            installationId, $"repos/{GitHubUrls.EscapeSegments(fullName)}/branches/{GitHubUrls.EscapeSegments(branch)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response);
        var body = await ReadAsync<BranchResponse>(response, cancellationToken);
        return body.Commit is null ? null : ToCommit(body.Commit);
    }

    public async Task<string?> GetFolderShaAsync(
        long installationId, string fullName, string commitSha, string path, CancellationToken cancellationToken)
    {
        var slash = path.LastIndexOf('/');
        var parent = slash < 0 ? string.Empty : path[..slash];
        var name = path[(slash + 1)..];
        var contents = parent.Length == 0 ? "contents" : $"contents/{GitHubUrls.EscapeSegments(parent)}";
        using var response = await SendAsInstallationAsync(
            installationId,
            $"repos/{GitHubUrls.EscapeSegments(fullName)}/{contents}?ref={Uri.EscapeDataString(commitSha)}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response);
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (body.RootElement.ValueKind != JsonValueKind.Array)
        {
            // The parent is a file.
            return null;
        }

        foreach (var entry in body.RootElement.EnumerateArray())
        {
            if (entry.TryGetProperty("name", out var entryName) && entryName.GetString() == name
                && entry.TryGetProperty("type", out var type) && type.GetString() == "dir"
                && entry.TryGetProperty("sha", out var sha))
            {
                return sha.GetString();
            }
        }

        return null;
    }

    public async Task<GitHubTree> GetTreeAsync(
        long installationId, string fullName, string treeSha, CancellationToken cancellationToken)
    {
        using var response = await SendAsInstallationAsync(
            installationId,
            $"repos/{GitHubUrls.EscapeSegments(fullName)}/git/trees/{Uri.EscapeDataString(treeSha)}?recursive=1",
            cancellationToken);
        EnsureSuccess(response);
        var body = await ReadAsync<TreeResponse>(response, cancellationToken);
        var entries = (body.Tree ?? [])
            .Where(e => e.Path is not null && e.Type is not null && e.Sha is not null)
            .Select(e => new GitHubTreeEntry(e.Path!, e.Type!, e.Sha!, e.Size ?? 0))
            .ToList();
        return new GitHubTree(entries, body.Truncated);
    }

    public async Task<byte[]> GetBlobAsync(long installationId, string fullName, string sha, CancellationToken cancellationToken)
    {
        using var response = await SendAsInstallationAsync(
            installationId,
            $"repos/{GitHubUrls.EscapeSegments(fullName)}/git/blobs/{Uri.EscapeDataString(sha)}",
            cancellationToken,
            RawMediaType);
        EnsureSuccess(response);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<GitHubCommitInfo?> GetLastCommitAsync(
        long installationId, string fullName, string branch, string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsInstallationAsync(
            installationId,
            $"repos/{GitHubUrls.EscapeSegments(fullName)}/commits?sha={Uri.EscapeDataString(branch)}&path={Uri.EscapeDataString(path)}&per_page=1",
            cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
        {
            return null;
        }

        EnsureSuccess(response);
        var commits = await ReadAsync<List<CommitResponse>>(response, cancellationToken);
        return commits.Count == 0 ? null : ToCommit(commits[0]);
    }

    public async Task<byte[]?> GetFileAsync(
        long installationId, string fullName, string commitSha, string path, int maxBytes, CancellationToken cancellationToken)
    {
        using var response = await SendAsInstallationAsync(
            installationId,
            $"repos/{GitHubUrls.EscapeSegments(fullName)}/contents/{GitHubUrls.EscapeSegments(path)}?ref={Uri.EscapeDataString(commitSha)}",
            cancellationToken,
            RawMediaType);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response);

        // A folder is answered with its JSON listing even when the raw content is asked for.
        if (response.Content.Headers.ContentType?.MediaType == "application/json"
            || response.Content.Headers.ContentLength > maxBytes)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    public void ForgetInstallation(long installationId) => tokens.Forget(installationId);

    /// <summary>
    /// Sends a GET as the installation. A 401 means the cached token was revoked (the app was uninstalled or the token
    /// rotated): the token is dropped and the request retried once with a fresh one.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsInstallationAsync(
        long installationId, string relativeUrl, CancellationToken cancellationToken, string? accept = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            var token = await tokens.GetAsync(installationId, ct => CreateInstallationTokenAsync(installationId, ct), cancellationToken);
            using var request = Get(relativeUrl, token);
            if (accept is not null)
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
            }

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

    private static GitHubCommitInfo ToCommit(CommitResponse commit)
    {
        var message = commit.Commit?.Message ?? string.Empty;
        var newline = message.IndexOfAny(['\r', '\n']);
        return new GitHubCommitInfo(
            commit.Sha,
            commit.Commit?.Tree?.Sha ?? string.Empty,
            newline < 0 ? message : message[..newline],
            commit.Commit?.Author?.Name ?? string.Empty,
            commit.Commit?.Author?.Date ?? DateTimeOffset.MinValue);
    }

    private sealed record BranchResponse(string? Name, CommitResponse? Commit);

    private sealed record CommitResponse(string Sha, CommitDetailResponse? Commit);

    private sealed record CommitDetailResponse(string? Message, CommitAuthorResponse? Author, ShaResponse? Tree);

    private sealed record CommitAuthorResponse(string? Name, DateTimeOffset? Date);

    private sealed record ShaResponse(string? Sha);

    private sealed record TreeResponse(IReadOnlyList<TreeEntryResponse>? Tree, bool Truncated);

    private sealed record TreeEntryResponse(string? Path, string? Type, string? Sha, long? Size);

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
