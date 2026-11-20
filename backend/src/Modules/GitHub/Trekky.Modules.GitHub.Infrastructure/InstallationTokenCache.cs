using Microsoft.Extensions.Caching.Memory;

namespace Trekky.Modules.GitHub.Infrastructure;

/// <summary>
/// Installation access tokens in this process's memory only (ADR-0028): reused until five minutes before GitHub's
/// expiry (tokens live one hour), never written to the database, logs or responses. Its own cache, so nothing else in
/// the app can read or evict the entries.
/// </summary>
internal sealed class InstallationTokenCache(TimeProvider time) : IDisposable
{
    /// <summary>A token is dropped this long before it expires, so a request never starts with one about to lapse.</summary>
    public static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(5);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public async Task<string> GetAsync(
        long installationId,
        Func<CancellationToken, Task<(string Token, DateTimeOffset ExpiresAt)>> create,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(create);
        if (_cache.TryGetValue(installationId, out string? cached) && cached is not null)
        {
            return cached;
        }

        var (token, expiresAt) = await create(cancellationToken);
        var lifetime = expiresAt - time.GetUtcNow() - RenewBefore;
        if (lifetime > TimeSpan.Zero)
        {
            _cache.Set(installationId, token, lifetime);
        }

        return token;
    }

    public void Forget(long installationId) => _cache.Remove(installationId);

    public void Dispose() => _cache.Dispose();
}
