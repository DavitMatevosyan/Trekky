using OpenIddict.Abstractions;

namespace Trekky.Modules.Identity.Auth;

/// <summary>Revokes every authorization and token of a user (logout, US-02 AC4).</summary>
internal sealed class SessionRevoker(
    IOpenIddictAuthorizationManager authorizations,
    IOpenIddictTokenManager tokens)
{
    public async Task RevokeAllAsync(string subject, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);

        // Materialize first: Npgsql allows one open reader per connection, and revoking writes.
        foreach (var token in await ToListAsync(tokens.FindBySubjectAsync(subject, cancellationToken), cancellationToken))
        {
            await tokens.TryRevokeAsync(token, cancellationToken);
        }

        foreach (var authorization in await ToListAsync(authorizations.FindBySubjectAsync(subject, cancellationToken), cancellationToken))
        {
            await authorizations.TryRevokeAsync(authorization, cancellationToken);
        }
    }

    private static async Task<List<object>> ToListAsync(IAsyncEnumerable<object> source, CancellationToken cancellationToken)
    {
        var list = new List<object>();
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            list.Add(item);
        }

        return list;
    }
}
