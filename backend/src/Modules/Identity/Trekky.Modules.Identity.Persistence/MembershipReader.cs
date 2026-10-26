using Microsoft.EntityFrameworkCore;

namespace Trekky.Modules.Identity.Persistence;

internal sealed record MembershipInfo(Guid OrgId, string Role);

/// <summary>Finds a user's organization and role when no tenant is known yet (authorize and token endpoints).</summary>
internal sealed class MembershipReader(IdentityDbContext db)
{
    /// <summary>The user's membership, or <c>null</c> if they have none. v0.1 users have exactly one.</summary>
    public async Task<MembershipInfo?> FindForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SetUserAsync(userId, cancellationToken);

        var membership = await db.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MembershipInfo(m.OrgId, m.Role))
            .FirstOrDefaultAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return membership;
    }
}
