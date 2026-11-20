using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Trekky.Api.Tests;

/// <summary>
/// A stand-in for github.com and api.github.com (TREK-74) on a local port, so the API's real HTTP client, headers and
/// URL encoding are exercised. It checks what GitHub checks: the app JWT's RS256 signature, issuer and lifetime,
/// installation tokens it issued, and user tokens from codes it knows. Tests add their own installations, users and
/// repositories with unique ids, so they can share one instance.
/// </summary>
public sealed class FakeGitHub : IAsyncDisposable
{
    public const string ClientId = "Iv23.trekky-test";
    public const string ClientSecret = "test-client-secret";
    public const string AppSlug = "trekky-test";

    private readonly RSA _key = RSA.Create(2048);
    private readonly ConcurrentDictionary<string, string> _codes = new();
    private readonly ConcurrentDictionary<string, string> _userTokens = new();
    private readonly ConcurrentDictionary<string, HashSet<long>> _userInstallations = new();
    private readonly ConcurrentDictionary<long, (string Login, string Type)> _installations = new();
    private readonly ConcurrentDictionary<long, List<FakeRepository>> _installationRepositories = new();
    private readonly ConcurrentDictionary<string, long> _installationTokens = new();
    private readonly ConcurrentDictionary<long, int> _tokenRequests = new();
    private readonly ConcurrentDictionary<long, bool> _failing = new();
    private readonly ConcurrentDictionary<string, FakeRepository> _repositories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Per user: the repositories they can reach and whether they administer each.</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _userRepositories = new();
    private readonly ConcurrentBag<string> _issuedUserTokens = [];
    private readonly ConcurrentBag<string> _revokedUserTokens = [];
    private WebApplication? _app;
    private long _nextId = Random.Shared.NextInt64(1_000_000, 9_000_000);

    public Uri BaseUrl { get; private set; } = new("http://127.0.0.1/");

    /// <summary>The app's private key, as the API reads it from configuration.</summary>
    public string PrivateKeyPem => _key.ExportRSAPrivateKeyPem();

    /// <summary>Every token this fake handed out, to check none of them reaches an API response.</summary>
    public IEnumerable<string> IssuedTokens => _installationTokens.Keys.Concat(_issuedUserTokens);

    /// <summary>User tokens the API revoked after its check.</summary>
    public IReadOnlyCollection<string> RevokedUserTokens => _revokedUserTokens;

    /// <summary>User tokens issued to <paramref name="user"/> that are still valid.</summary>
    public int ActiveUserTokens(string user) => _userTokens.Values.Count(owner => owner == user);

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _app = builder.Build();
        Map(_app);
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        BaseUrl = new Uri(address.TrimEnd('/') + "/");
    }

    public long NextId() => Interlocked.Increment(ref _nextId);

    /// <summary>An installation of the app on <paramref name="account"/>, reaching <paramref name="repositories"/>.</summary>
    public long AddInstallation(string account, string accountType = "Organization", params FakeRepository[] repositories)
    {
        var id = NextId();
        _installations[id] = (account, accountType);
        _installationRepositories[id] = [.. repositories];
        foreach (var repository in repositories)
        {
            _repositories[repository.FullName] = repository;
        }

        return id;
    }

    /// <summary>A repository that exists on GitHub (public) but that no installation was given.</summary>
    public void AddPublicRepository(FakeRepository repository) => _repositories[repository.FullName] = repository;

    /// <summary>
    /// A GitHub user who administers every repository of <paramref name="installationIds"/> (as whoever installed it);
    /// returns a fresh install code for them.
    /// </summary>
    public string CodeFor(string user, params long[] installationIds)
    {
        var repositories = _userRepositories.GetOrAdd(user, _ => new(StringComparer.OrdinalIgnoreCase));
        foreach (var repository in installationIds.SelectMany(id => _installationRepositories.GetValueOrDefault(id) ?? []))
        {
            repositories[repository.FullName] = true;
        }

        return CodeWithAccess(user, installationIds);
    }

    /// <summary>A GitHub user who can only read <paramref name="readable"/> in the installation; returns an install code.</summary>
    public string CodeForReader(string user, long installationId, params string[] readable)
    {
        var repositories = _userRepositories.GetOrAdd(user, _ => new(StringComparer.OrdinalIgnoreCase));
        foreach (var name in readable)
        {
            repositories[name] = false;
        }

        return CodeWithAccess(user, installationId);
    }

    private string CodeWithAccess(string user, params long[] installationIds)
    {
        _userInstallations.AddOrUpdate(user, _ => [.. installationIds], (_, set) =>
        {
            lock (set)
            {
                set.UnionWith(installationIds);
            }

            return set;
        });
        var code = $"code-{Guid.NewGuid():N}"[..20];
        _codes[code] = user;
        return code;
    }

    public int TokenRequests(long installationId) => _tokenRequests.GetValueOrDefault(installationId);

    /// <summary>The app was uninstalled on GitHub.</summary>
    public void Uninstall(long installationId) => _installations.TryRemove(installationId, out _);

    /// <summary>GitHub answers 500 for the installation's repository calls.</summary>
    public void Fail(long installationId) => _failing[installationId] = true;

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        _key.Dispose();
    }

    private void Map(WebApplication app)
    {
        app.MapPost("/login/oauth/access_token", async (HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            if (form["client_id"] != ClientId || form["client_secret"] != ClientSecret)
            {
                return Results.Json(new { error = "incorrect_client_credentials" });
            }

            // A code works once, like GitHub's.
            if (!_codes.TryRemove(form["code"].ToString(), out var user))
            {
                return Results.Json(new { error = "bad_verification_code" });
            }

            var token = $"ghu_{Guid.NewGuid():N}";
            _userTokens[token] = user;
            _issuedUserTokens.Add(token);
            return Results.Json(new { access_token = token, token_type = "bearer", scope = "" });
        });

        // The user's own repositories in an installation, with their permission on each (GitHub's "permissions" hash).
        app.MapGet("/user/installations/{id:long}/repositories", (HttpRequest request, long id, int page) =>
        {
            if (!_userTokens.TryGetValue(Bearer(request) ?? string.Empty, out var user))
            {
                return Results.StatusCode(401);
            }

            if (!_installations.ContainsKey(id) || !(_userInstallations.TryGetValue(user, out var set) && Snapshot(set).Contains(id)))
            {
                return Results.NotFound();
            }

            var mine = _userRepositories.GetValueOrDefault(user);
            var reachable = (_installationRepositories.GetValueOrDefault(id) ?? [])
                .Where(r => mine is not null && mine.ContainsKey(r.FullName))
                .ToList();
            var items = reachable.Skip((page - 1) * 100).Take(100).Select(r => new
            {
                id = r.Id,
                full_name = r.FullName,
                default_branch = r.Branches.FirstOrDefault() ?? "main",
                @private = r.Private,
                html_url = $"https://github.com/{r.FullName}",
                permissions = new { admin = mine![r.FullName], push = true, pull = true },
            }).ToArray();
            return Results.Json(new { total_count = reachable.Count, repositories = items });
        });

        app.MapDelete("/applications/{clientId}/token", async (HttpRequest request, string clientId) =>
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
            if (clientId != ClientId || request.Headers.Authorization.ToString() != $"Basic {basic}")
            {
                return Results.StatusCode(401);
            }

            using var body = await JsonDocument.ParseAsync(request.Body);
            var token = body.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
            if (!_userTokens.TryRemove(token, out _))
            {
                return Results.NotFound();
            }

            _revokedUserTokens.Add(token);
            return Results.NoContent();
        });

        app.MapGet("/user/installations", (HttpRequest request, int page) =>
        {
            if (!_userTokens.TryGetValue(Bearer(request) ?? string.Empty, out var user))
            {
                return Results.StatusCode(401);
            }

            var ids = _userInstallations.TryGetValue(user, out var set) ? Snapshot(set) : [];
            var installations = page == 1
                ? ids.Where(_installations.ContainsKey).Select(id => new { id, account = Account(id) }).ToArray()
                : [];
            return Results.Json(new { total_count = installations.Length, installations });
        });

        app.MapGet("/app/installations/{id:long}", (HttpRequest request, long id) =>
        {
            if (!ValidJwt(request))
            {
                return Results.StatusCode(401);
            }

            return _installations.ContainsKey(id) ? Results.Json(new { id, account = Account(id) }) : Results.NotFound();
        });

        app.MapPost("/app/installations/{id:long}/access_tokens", (HttpRequest request, long id) =>
        {
            if (!ValidJwt(request))
            {
                return Results.StatusCode(401);
            }

            if (!_installations.ContainsKey(id))
            {
                return Results.NotFound();
            }

            _tokenRequests.AddOrUpdate(id, 1, (_, count) => count + 1);
            var token = $"ghs_{Guid.NewGuid():N}";
            _installationTokens[token] = id;
            return Results.Json(new { token, expires_at = DateTimeOffset.UtcNow.AddHours(1) }, statusCode: 201);
        });

        app.MapGet("/installation/repositories", (HttpRequest request, int page) => AsInstallation(request, id =>
        {
            var all = _installationRepositories.GetValueOrDefault(id) ?? [];
            var items = all.Skip((page - 1) * 100).Take(100).Select(Json).ToArray();
            return Results.Json(new { total_count = all.Count, repositories = items });
        }));

        app.MapGet("/repos/{owner}/{name}/branches/{**branch}", (HttpRequest request, string owner, string name, string branch)
            => AsInstallation(request, id => Reachable(id, owner, name) is { } repository && repository.Branches.Contains(branch)
                ? Results.Json(new { name = branch })
                : Results.NotFound()));

        app.MapGet("/repos/{owner}/{name}/contents/{**path}", (HttpRequest request, string owner, string name, string? path, string @ref)
            => AsInstallation(request, id => Contents(id, owner, name, path ?? string.Empty, @ref)));

        app.MapGet("/repos/{owner}/{name}/contents", (HttpRequest request, string owner, string name, string @ref)
            => AsInstallation(request, id => Contents(id, owner, name, string.Empty, @ref)));
    }

    private IResult Contents(long installationId, string owner, string name, string path, string branch)
    {
        if (Reachable(installationId, owner, name) is not { } repository || !repository.Branches.Contains(branch))
        {
            return Results.NotFound();
        }

        if (path.Length == 0 || repository.Folders.Contains(path))
        {
            return Results.Json(new[] { new { type = "file", name = "README.md", path = $"{path}/README.md" } });
        }

        return repository.Files.Contains(path)
            ? Results.Json(new { type = "file", name = path.Split('/')[^1], path })
            : Results.NotFound();
    }

    /// <summary>Runs <paramref name="handle"/> for the installation the bearer token belongs to, or answers like GitHub.</summary>
    private IResult AsInstallation(HttpRequest request, Func<long, IResult> handle)
    {
        if (!_installationTokens.TryGetValue(Bearer(request) ?? string.Empty, out var id) || !_installations.ContainsKey(id))
        {
            return Results.StatusCode(401);
        }

        return _failing.ContainsKey(id) ? Results.StatusCode(500) : handle(id);
    }

    /// <summary>The repository when the installation was given it (installation tokens see nothing else).</summary>
    private FakeRepository? Reachable(long installationId, string owner, string name)
        => (_installationRepositories.GetValueOrDefault(installationId) ?? [])
            .FirstOrDefault(r => string.Equals(r.FullName, $"{owner}/{name}", StringComparison.OrdinalIgnoreCase));

    private object Account(long installationId)
    {
        var (login, type) = _installations[installationId];
        return new { login, type };
    }

    private static object Json(FakeRepository repository)
        => new
        {
            id = repository.Id,
            full_name = repository.FullName,
            default_branch = repository.Branches.FirstOrDefault() ?? "main",
            @private = repository.Private,
            html_url = $"https://github.com/{repository.FullName}",
        };

    private static long[] Snapshot(HashSet<long> set)
    {
        lock (set)
        {
            return [.. set];
        }
    }

    private static string? Bearer(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.Ordinal) ? header["Bearer ".Length..] : null;
    }

    /// <summary>GitHub's checks on an app JWT: RS256 by the app's key, issuer = client id, at most 10 minutes long, not expired.</summary>
    private bool ValidJwt(HttpRequest request)
    {
        var parts = (Bearer(request) ?? string.Empty).Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        try
        {
            var signed = _key.VerifyData(
                Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
                Base64Url.DecodeFromChars(parts[2]),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
            using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
            var iat = payload.RootElement.GetProperty("iat").GetInt64();
            var exp = payload.RootElement.GetProperty("exp").GetInt64();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return signed
                && header.RootElement.GetProperty("alg").GetString() == "RS256"
                && payload.RootElement.GetProperty("iss").GetString() == ClientId
                && exp - iat <= 600
                && iat <= now
                && exp > now;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }
}

