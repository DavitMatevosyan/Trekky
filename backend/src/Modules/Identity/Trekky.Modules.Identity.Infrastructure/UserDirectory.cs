using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Application.Members;
using Trekky.Modules.Identity.Persistence;
using Trekky.SharedKernel;

namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>
/// Users are not tenant-scoped, so the lookup goes through <c>memberships</c>, which RLS limits to the request's
/// organization: a user of another organization is never found.
/// </summary>
internal sealed class UserDirectory(IdentityDbContext db, ITenantContext tenant) : IUserDirectory
{
    public async Task<IReadOnlyList<Member>> ListMembersAsync(CancellationToken cancellationToken)
    {
        var orgId = tenant.OrgId ?? throw new InvalidOperationException("Listing members needs a tenant.");

        // The organization by name as well as by RLS: user_self_read (ADR-0007) also shows the caller's own other memberships.
        var members = await db.Memberships
            .AsNoTracking()
            .Where(m => m.OrgId == orgId && m.RemovedAt == null)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { u.Id, u.DisplayName, u.Email, m.Role, m.Version })
            .ToListAsync(cancellationToken);
        return members
            .OrderBy(m => m.DisplayName, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(m => m.Id)
            // Every member is a person until bots arrive (TREK-49).
            .Select(m => new Member(new UserRef(m.Id, m.DisplayName, m.Email), m.Role, MemberKinds.Person, MemberVersion.ETagFor(m.Version)))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, UserRef>> FindMembersAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserRef>();
        }

        var orgId = tenant.OrgId ?? throw new InvalidOperationException("Finding members needs a tenant.");
        var ids = userIds.Distinct().ToList();
        // The organization by name as well as by RLS, as in ListMembersAsync. Removed members too (TREK-48): their past
        // work keeps their name.
        return await db.Memberships
            .AsNoTracking()
            .Where(m => m.OrgId == orgId && ids.Contains(m.UserId))
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new UserRef(u.Id, u.DisplayName, u.Email))
            // Once each, even where another policy (user_self_read, ADR-0007) would show a user's other memberships.
            .Distinct()
            .ToDictionaryAsync(u => u.Id, cancellationToken);
    }

    public async Task<bool> IsMemberAsync(Guid userId, CancellationToken cancellationToken)
    {
        var orgId = tenant.OrgId ?? throw new InvalidOperationException("Checking a member needs a tenant.");
        return await db.Memberships.AnyAsync(m => m.OrgId == orgId && m.UserId == userId && m.RemovedAt == null, cancellationToken);
    }
}
