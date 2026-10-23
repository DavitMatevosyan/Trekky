using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using Trekky.Modules.Identity.Domain;

namespace Trekky.Modules.Identity.Auth;

/// <summary>
/// Logs a user out everywhere (US-02 AC4): revokes every OpenIddict authorization of the user, which
/// invalidates the refresh and access tokens chained to them (authorization entry validation is on in
/// both the server and the validation handler), and rotates the security stamp so existing session
/// cookies stop working at <c>/connect/authorize</c>.
/// </summary>
internal sealed class SessionRevoker(
    IOpenIddictAuthorizationManager authorizations,
    UserManager<User> users)
{
    public async Task RevokeAllAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Materialize first: Npgsql allows one open reader per connection, and revoking writes.
        var active = new List<object>();
        await foreach (var authorization in authorizations.FindBySubjectAsync(user.Id.ToString(), cancellationToken))
        {
            active.Add(authorization);
        }

        foreach (var authorization in active)
        {
            await authorizations.TryRevokeAsync(authorization, cancellationToken);
        }

        await users.UpdateSecurityStampAsync(user);
    }
}
