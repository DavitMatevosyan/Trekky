using Microsoft.EntityFrameworkCore;
using Trekky.Persistence.Rls;

namespace Trekky.Modules.Identity.Persistence;

/// <summary>
/// Sets RLS session settings for the current transaction only (<c>set_config(..., true)</c>), so they end
/// with the transaction and can never leak to the next user of a pooled connection.
/// Call inside an open transaction.
/// </summary>
internal static class TransactionSettings
{
    /// <summary>Acts as <paramref name="orgId"/> for this transaction (sign-up writes the new organization before any token exists).</summary>
    public static Task SetTenantAsync(this IdentityDbContext db, Guid orgId, CancellationToken cancellationToken)
        => SetAsync(db, TenantRowLevelSecurity.SettingName, orgId, cancellationToken);

    /// <summary>Lets this transaction read <paramref name="userId"/>'s memberships (policy <c>user_self_read</c>, ADR-0007).</summary>
    public static Task SetUserAsync(this IdentityDbContext db, Guid userId, CancellationToken cancellationToken)
        => SetAsync(db, UserRowLevelSecurity.SettingName, userId, cancellationToken);

    /// <summary>Lets this transaction read the invite with this token hash (policy <c>invite_token_read</c>, ADR-0027).</summary>
    public static Task SetInviteTokenHashAsync(this IdentityDbContext db, byte[] tokenHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        return SetAsync(db, InviteRowLevelSecurity.TokenHashSettingName, Convert.ToHexStringLower(tokenHash), cancellationToken);
    }

    private static Task SetAsync(IdentityDbContext db, string setting, Guid value, CancellationToken cancellationToken)
        => SetAsync(db, setting, value.ToString(), cancellationToken);

    private static async Task SetAsync(IdentityDbContext db, string setting, string value, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException($"'{setting}' can only be set inside a transaction.");
        }

        await db.Database.ExecuteSqlAsync($"SELECT set_config({setting}, {value}, true)", cancellationToken);
    }
}
