using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Persistence;

namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>
/// Users are not tenant-scoped, so the lookup goes through <c>memberships</c>, which RLS limits to the request's
/// organization: a user of another organization is never found.
/// </summary>
internal sealed class UserDirectory(IdentityDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<Guid, UserRef>> FindMembersAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserRef>();
        }

        var ids = userIds.Distinct().ToList();
        return await db.Memberships
            .AsNoTracking()
            .Where(m => ids.Contains(m.UserId))
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new UserRef(u.Id, u.DisplayName, u.Email))
            // Once each, even where another policy (user_self_read, ADR-0007) would show a user's other memberships.
            .Distinct()
            .ToDictionaryAsync(u => u.Id, cancellationToken);
    }
}
