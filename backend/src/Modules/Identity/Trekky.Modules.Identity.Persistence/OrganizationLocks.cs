using Microsoft.EntityFrameworkCore;

namespace Trekky.Modules.Identity.Persistence;

/// <summary>Row locks that keep the member rules true under concurrent requests (TREK-48).</summary>
internal static class OrganizationLocks
{
    /// <summary>
    /// Locks the organization's row until the transaction ends, so changes to its members run one at a time and two
    /// Owners cannot each demote the other. <c>FOR NO KEY UPDATE</c> does not block the foreign-key checks of new invites
    /// and memberships, which take <c>FOR KEY SHARE</c>. Call inside an open transaction.
    /// </summary>
    public static async Task LockOrganizationAsync(this IdentityDbContext db, Guid orgId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The organization can only be locked inside a transaction.");
        }

        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM identity.organizations WHERE id = {orgId} FOR NO KEY UPDATE", cancellationToken);
    }
}
