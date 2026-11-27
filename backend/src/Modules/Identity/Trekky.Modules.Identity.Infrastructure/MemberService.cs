using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Application.Members;
using Trekky.Modules.Identity.Domain;
using Trekky.Modules.Identity.Persistence;
using Trekky.Persistence.Outbox;
using Trekky.SharedKernel;
using Trekky.SharedKernel.Events;

namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>
/// Role changes and removals (TREK-48, ADR-0026). Each runs in one transaction that holds the organization's lock, so the
/// last-Owner check cannot be raced; the change, its activity event and the member's token revocation commit together.
/// </summary>
internal sealed class MemberService(
    IdentityDbContext db,
    ITenantContext tenant,
    UserManager<User> users,
    SessionRevoker sessions,
    TimeProvider time) : IMemberService
{
    public async Task<(MemberChangeOutcome Outcome, Member? Member)> ChangeRoleAsync(
        Guid userId, string role, string ifMatch, MemberActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(ifMatch);
        ArgumentNullException.ThrowIfNull(actor);
        var orgId = RequireTenant();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockOrganizationAsync(orgId, cancellationToken);

        var membership = await FindActiveAsync(orgId, userId, cancellationToken);
        if (membership is null)
        {
            return (MemberChangeOutcome.NotFound, null);
        }

        if (!MemberRules.CanChangeRole(await CurrentRoleAsync(orgId, actor, cancellationToken), membership.Role, role))
        {
            return (MemberChangeOutcome.RoleNotAllowed, null);
        }

        var user = await UserAsync(userId);
        if (!MemberVersion.IfMatchAccepts(ifMatch, membership.Version))
        {
            return (MemberChangeOutcome.Stale, ToContract(user, membership));
        }

        if (membership.Role == role)
        {
            // Nothing changes: no write, no event, and the member's tokens stay valid.
            return (MemberChangeOutcome.Done, ToContract(user, membership));
        }

        if (MemberRules.LeavesNoOwner(membership.Role, role, await OwnersAsync(orgId, cancellationToken)))
        {
            return (MemberChangeOutcome.LastOwner, null);
        }

        var from = membership.Role;
        membership.Role = role;
        db.AddActivityEvent(Event(
            MemberEventTypes.MemberRoleChanged, orgId, user, actor, new JsonObject { ["from"] = from, ["to"] = role }));
        await db.SaveChangesAsync(cancellationToken);

        // Their tokens carry the old role (ADR-0026): revoked, their next request fails, and the web app signs in again
        // silently with the session cookie, which is kept, getting a token with the new role.
        await sessions.RevokeTokensAsync(user, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (MemberChangeOutcome.Done, ToContract(user, membership));
    }

    public async Task<MemberChangeOutcome> RemoveAsync(Guid userId, MemberActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var orgId = RequireTenant();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockOrganizationAsync(orgId, cancellationToken);

        var membership = await FindActiveAsync(orgId, userId, cancellationToken);
        if (membership is null)
        {
            return MemberChangeOutcome.NotFound;
        }

        if (!MemberRules.CanRemove(await CurrentRoleAsync(orgId, actor, cancellationToken), membership.Role))
        {
            return MemberChangeOutcome.RoleNotAllowed;
        }

        if (MemberRules.LeavesNoOwner(membership.Role, newRole: null, await OwnersAsync(orgId, cancellationToken)))
        {
            return MemberChangeOutcome.LastOwner;
        }

        var user = await UserAsync(userId);
        membership.RemovedAt = time.GetUtcNow();
        db.AddActivityEvent(Event(MemberEventTypes.MemberRemoved, orgId, user, actor, new JsonObject { ["role"] = membership.Role }));
        await db.SaveChangesAsync(cancellationToken);

        // Every token and session ends: refresh tokens are revoked, access tokens fail on their next request (token entries
        // are checked on every call), and the session cookie stops working (security stamp).
        await sessions.RevokeAllAsync(user, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MemberChangeOutcome.Done;
    }

    private Guid RequireTenant() => tenant.OrgId ?? throw new InvalidOperationException("Managing members needs a tenant.");

    /// <summary>The organization by name as well as by RLS: user_self_read (ADR-0007) also shows the caller's own membership.</summary>
    private Task<Membership?> FindActiveAsync(Guid orgId, Guid userId, CancellationToken cancellationToken)
        => db.Memberships.FirstOrDefaultAsync(
            m => m.OrgId == orgId && m.UserId == userId && m.RemovedAt == null, cancellationToken);

    /// <summary>
    /// The caller's role as it is now, read under the organization's lock: the token's role can be up to 15 minutes old,
    /// or the caller may have been demoted or removed while this request waited for the lock. <c>null</c> if removed.
    /// </summary>
    private async Task<string?> CurrentRoleAsync(Guid orgId, MemberActor actor, CancellationToken cancellationToken)
        => (await db.Memberships
                .AsNoTracking()
                .Where(m => m.OrgId == orgId && m.UserId == actor.UserId && m.RemovedAt == null)
                .Select(m => m.Role)
                .ToListAsync(cancellationToken))
            .SingleOrDefault();

    private Task<int> OwnersAsync(Guid orgId, CancellationToken cancellationToken)
        => db.Memberships.CountAsync(m => m.OrgId == orgId && m.RemovedAt == null && m.Role == Roles.Owner, cancellationToken);

    private async Task<User> UserAsync(Guid userId)
        => await users.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"Membership {userId} has no user.");

    private ActivityEnvelope Event(string eventType, Guid orgId, User user, MemberActor actor, JsonObject payload)
    {
        payload["userId"] = user.Id;
        payload["email"] = user.Email;
        payload["displayName"] = user.DisplayName;
        return ActivityEnvelope.Create(
            eventType,
            time.GetUtcNow(),
            orgId,
            EventActor.User(actor.UserId),
            new EventEntity(Membership.EntityType, user.Id, null),
            null,
            payload);
    }

    private static Member ToContract(User user, Membership membership)
        => new(
            new UserRef(user.Id, user.DisplayName, user.Email),
            membership.Role,
            MemberKinds.Person,
            MemberVersion.ETagFor(membership.Version));
}
