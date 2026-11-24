using Trekky.Modules.GitHub.Infrastructure;

namespace Trekky.Modules.GitHub.Tests;

public sealed class InstallationTokenCacheTests
{
    [Fact]
    public async Task A_token_is_reused_until_it_is_forgotten()
    {
        using var cache = new InstallationTokenCache(TimeProvider.System);
        var created = 0;
        Task<(string, DateTimeOffset)> Create(CancellationToken _)
            => Task.FromResult(($"token-{++created}", DateTimeOffset.UtcNow.AddHours(1)));

        var first = await cache.GetAsync(7, Create, CancellationToken.None);
        var second = await cache.GetAsync(7, Create, CancellationToken.None);
        cache.Forget(7);
        var third = await cache.GetAsync(7, Create, CancellationToken.None);

        Assert.Equal("token-1", first);
        Assert.Equal("token-1", second);
        Assert.Equal("token-2", third);
    }

    [Fact]
    public async Task Each_installation_has_its_own_token()
    {
        using var cache = new InstallationTokenCache(TimeProvider.System);

        var one = await cache.GetAsync(1, _ => Task.FromResult(("one", DateTimeOffset.UtcNow.AddHours(1))), CancellationToken.None);
        var two = await cache.GetAsync(2, _ => Task.FromResult(("two", DateTimeOffset.UtcNow.AddHours(1))), CancellationToken.None);

        Assert.Equal(("one", "two"), (one, two));
    }

    [Fact]
    public async Task A_token_about_to_expire_is_used_once_and_not_kept()
    {
        using var cache = new InstallationTokenCache(TimeProvider.System);
        var created = 0;
        Task<(string, DateTimeOffset)> Create(CancellationToken _)
            => Task.FromResult(($"token-{++created}", DateTimeOffset.UtcNow.Add(InstallationTokenCache.RenewBefore / 2)));

        await cache.GetAsync(9, Create, CancellationToken.None);
        var next = await cache.GetAsync(9, Create, CancellationToken.None);

        Assert.Equal("token-2", next);
    }
}
