using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trekky.Modules.Identity.Application.Accounts;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Application.Invites;
using Trekky.Modules.Identity.Domain;
using Trekky.Modules.Identity.Persistence;
using Trekky.Persistence.Outbox;
using Trekky.SharedKernel;
using Trekky.SharedKernel.Events;
using InviteEntity = Trekky.Modules.Identity.Domain.Invite;

namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>
/// Invites (TREK-47, ADR-0027). Managing them runs as the request's tenant; the invite page and accepting run before any
/// tenant is known and find the invite by its token hash (policy <c>invite_token_read</c>), then act as its organization.
/// </summary>
internal sealed class InviteService(
    IdentityDbContext db,
    ITenantContext tenant,
    UserManager<User> users,
    SignInManager<User> signIn,
    IUserDirectory directory,
    IInviteMailer mailer,
    InviteLinks links,
    TimeProvider time) : IInviteService
{
    public async Task<InviteList> ListAsync(CancellationToken cancellationToken)
    {
        RequireTenant();
        var invites = await db.Invites
            .AsNoTracking()
            .Where(i => i.AcceptedAt == null && i.RevokedAt == null)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .ToListAsync(cancellationToken);

        var inviters = await directory.FindMembersAsync(invites.Select(i => i.InvitedBy).ToList(), cancellationToken);
        var now = time.GetUtcNow();
        return new InviteList(invites.Select(i => ToContract(i, inviters.GetValueOrDefault(i.InvitedBy), now)).ToList());
    }

    public async Task<(CreateInviteOutcome Outcome, SentInvite? Sent)> CreateAsync(
        string email, string role, InviteActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(actor);
        var orgId = RequireTenant();
        email = email.Trim();

        if (!InviteRoles.CanManage(actor.Role, role))
        {
            return (CreateInviteOutcome.RoleNotAllowed, null);
        }

        // One organization per user (TREK-46): an address with an account cannot join another organization.
        if (await users.FindByEmailAsync(email) is { } existing)
        {
            var membership = await db.Memberships
                .AsNoTracking()
                .Where(m => m.OrgId == orgId && m.UserId == existing.Id)
                .Select(m => new { m.RemovedAt })
                .FirstOrDefaultAsync(cancellationToken);
            var outcome = membership is null ? CreateInviteOutcome.EmailHasAccount
                : membership.RemovedAt is null ? CreateInviteOutcome.AlreadyMember
                : CreateInviteOutcome.FormerMember;
            return (outcome, null);
        }

        var normalized = InviteEntity.Normalize(email);
        if (await db.Invites.AnyAsync(
                i => i.NormalizedEmail == normalized && i.AcceptedAt == null && i.RevokedAt == null, cancellationToken))
        {
            return (CreateInviteOutcome.AlreadyInvited, null);
        }

        var now = time.GetUtcNow();
        var (invite, token) = InviteEntity.Create(orgId, email, role, actor.UserId, now);
        db.Invites.Add(invite);
        db.AddActivityEvent(Event(InviteEventTypes.InviteSent, invite, actor.UserId, now, resent: false));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: IdentityDbContext.OpenInviteIndexName,
            })
        {
            // A concurrent invite for the same address won the race.
            return (CreateInviteOutcome.AlreadyInvited, null);
        }

        return (CreateInviteOutcome.Created, await SendAsync(invite, token, actor.UserId, cancellationToken));
    }

    public async Task<(InviteChangeOutcome Outcome, SentInvite? Sent)> ResendAsync(
        Guid inviteId, InviteActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var (outcome, invite) = await FindToChangeAsync(inviteId, actor, cancellationToken);
        if (invite is null)
        {
            return (outcome, null);
        }

        var now = time.GetUtcNow();
        var token = invite.Resend(now);
        db.AddActivityEvent(Event(InviteEventTypes.InviteSent, invite, actor.UserId, now, resent: true));
        if (!await TrySaveAsync(cancellationToken))
        {
            return (InviteChangeOutcome.Conflict, null);
        }

        return (InviteChangeOutcome.Done, await SendAsync(invite, token, actor.UserId, cancellationToken));
    }

    public async Task<InviteChangeOutcome> RevokeAsync(Guid inviteId, InviteActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var (outcome, invite) = await FindToChangeAsync(inviteId, actor, cancellationToken);
        if (invite is null)
        {
            return outcome;
        }

        var now = time.GetUtcNow();
        invite.Revoke(now);
        db.AddActivityEvent(Event(InviteEventTypes.InviteRevoked, invite, actor.UserId, now));
        return await TrySaveAsync(cancellationToken) ? InviteChangeOutcome.Done : InviteChangeOutcome.Conflict;
    }

    public async Task<(InviteLinkOutcome Outcome, InvitePreview? Preview)> PreviewAsync(
        string token, CancellationToken cancellationToken)
    {
        if (!InviteToken.IsWellFormed(token))
        {
            return (InviteLinkOutcome.NotFound, null);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var invite = await FindByTokenAsync(token, tracked: false, cancellationToken);
        var outcome = LinkOutcome(invite);
        if (invite is null || outcome != InviteLinkOutcome.Valid)
        {
            await transaction.CommitAsync(cancellationToken);
            return (outcome, null);
        }

        await db.SetTenantAsync(invite.OrgId, cancellationToken);
        var organization = await db.Organizations
            .AsNoTracking()
            .Where(o => o.Id == invite.OrgId)
            .Select(o => new OrganizationRef(o.Id, o.Name))
            .FirstAsync(cancellationToken);
        var inviter = await InviterNameAsync(invite, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (InviteLinkOutcome.Valid, new InvitePreview(invite.Email, invite.Role, organization, inviter, invite.ExpiresAt));
    }

    public async Task<(InviteLinkOutcome Outcome, Me? Me)> AcceptAsync(
        string token, string password, string? displayName, CancellationToken cancellationToken)
    {
        if (!InviteToken.IsWellFormed(token))
        {
            return (InviteLinkOutcome.NotFound, null);
        }

        var now = time.GetUtcNow();
        User user;
        InviteEntity? invite;
        OrganizationRef organization;

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            invite = await FindByTokenAsync(token, tracked: true, cancellationToken);
            var outcome = LinkOutcome(invite);
            if (invite is null || outcome != InviteLinkOutcome.Valid)
            {
                return (outcome, null);
            }

            // From here on act as the invite's organization: the membership, the accepted invite and the event are its rows.
            await db.SetTenantAsync(invite.OrgId, cancellationToken);

            var name = displayName?.Trim();
            user = new User
            {
                Email = invite.Email,
                UserName = invite.Email,
                DisplayName = string.IsNullOrEmpty(name) ? SignUpInput.DefaultDisplayName(invite.Email) : name,
                // Not confirmed: the inviter is also given the link (ADR-0027), so using it proves nothing about the inbox.
                CreatedAt = now,
            };

            try
            {
                var created = await users.CreateAsync(user, password);
                if (!created.Succeeded)
                {
                    if (created.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
                                                       or nameof(IdentityErrorDescriber.DuplicateUserName)))
                    {
                        return (InviteLinkOutcome.EmailHasAccount, null);
                    }

                    // Input was validated up front, so anything else is a bug, not a user error.
                    throw new InvalidOperationException(
                        "Creating the invited user failed: " + string.Join(", ", created.Errors.Select(e => e.Code)));
                }

                db.Memberships.Add(new Membership { OrgId = invite.OrgId, UserId = user.Id, Role = invite.Role, CreatedAt = now });
                invite.Accept(user.Id, now);
                db.AddActivityEvent(ActivityEnvelope.Create(
                    InviteEventTypes.InviteAccepted,
                    now,
                    invite.OrgId,
                    EventActor.User(user.Id),
                    new EventEntity(InviteEntity.EntityType, invite.Id, null),
                    null,
                    new JsonObject { ["email"] = invite.Email, ["role"] = invite.Role, ["userId"] = user.Id }));
                await db.SaveChangesAsync(cancellationToken);

                organization = await db.Organizations
                    .AsNoTracking()
                    .Where(o => o.Id == invite.OrgId)
                    .Select(o => new OrganizationRef(o.Id, o.Name))
                    .FirstAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // The invite was withdrawn, resent or accepted while this request ran: nothing of this one is kept.
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                return (await CurrentLinkOutcomeAsync(token, cancellationToken), null);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // A concurrent sign-up or accept with the same email won the race.
                return (InviteLinkOutcome.EmailHasAccount, null);
            }
        }

        await signIn.SignInAsync(user, isPersistent: false);
        return (InviteLinkOutcome.Valid, new Me(new UserRef(user.Id, user.DisplayName, user.Email), organization, invite.Role));
    }

    /// <summary>Saves; <c>false</c> if the invite changed since it was read (xmin), with nothing written.</summary>
    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    /// <summary>What the link leads to now, after a concurrent change; an invite that still looks pending counts as used.</summary>
    private async Task<InviteLinkOutcome> CurrentLinkOutcomeAsync(string token, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var outcome = LinkOutcome(await FindByTokenAsync(token, tracked: false, cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return outcome == InviteLinkOutcome.Valid ? InviteLinkOutcome.Used : outcome;
    }

    private Guid RequireTenant() => tenant.OrgId ?? throw new InvalidOperationException("Managing invites needs a tenant.");

    /// <summary>The open invite the caller may change, or why not.</summary>
    private async Task<(InviteChangeOutcome Outcome, InviteEntity? Invite)> FindToChangeAsync(
        Guid inviteId, InviteActor actor, CancellationToken cancellationToken)
    {
        RequireTenant();
        var invite = await db.Invites.FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken);
        if (invite is null)
        {
            return (InviteChangeOutcome.NotFound, null);
        }

        if (!InviteRoles.CanManage(actor.Role, invite.Role))
        {
            return (InviteChangeOutcome.RoleNotAllowed, null);
        }

        return invite.IsOpen ? (InviteChangeOutcome.Done, invite) : (InviteChangeOutcome.NotOpen, null);
    }

    /// <summary>Call inside a transaction: the token hash setting is transaction-local.</summary>
    private async Task<InviteEntity?> FindByTokenAsync(string token, bool tracked, CancellationToken cancellationToken)
    {
        var hash = InviteToken.Hash(token);
        await db.SetInviteTokenHashAsync(hash, cancellationToken);
        var query = tracked ? db.Invites : db.Invites.AsNoTracking();
        return await query.FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);
    }

    private InviteLinkOutcome LinkOutcome(InviteEntity? invite)
        => invite?.StateAt(time.GetUtcNow()) switch
        {
            null => InviteLinkOutcome.NotFound,
            InviteState.Pending => InviteLinkOutcome.Valid,
            InviteState.Expired => InviteLinkOutcome.Expired,
            InviteState.Revoked => InviteLinkOutcome.Revoked,
            _ => InviteLinkOutcome.Used,
        };

    private async Task<string?> InviterNameAsync(InviteEntity invite, CancellationToken cancellationToken)
        => await db.Users.AsNoTracking()
            .Where(u => u.Id == invite.InvitedBy)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Emails the link after the invite is saved; a mail failure leaves the invite in place (ADR-0027).</summary>
    private async Task<SentInvite> SendAsync(InviteEntity invite, string token, Guid actorId, CancellationToken cancellationToken)
    {
        var acceptUrl = links.AcceptUrl(token);
        var organizationName = await db.Organizations.AsNoTracking()
            .Where(o => o.Id == invite.OrgId)
            .Select(o => o.Name)
            .FirstAsync(cancellationToken);
        var inviters = await directory.FindMembersAsync([actorId], cancellationToken);
        var inviter = inviters.GetValueOrDefault(actorId);

        var emailSent = await mailer.SendAsync(
            new InviteEmail(invite.Email, organizationName, inviter?.DisplayName ?? organizationName, invite.Role, acceptUrl, invite.ExpiresAt),
            cancellationToken);

        return new SentInvite(ToContract(invite, inviter, time.GetUtcNow()), acceptUrl, emailSent);
    }

    private static ActivityEnvelope Event(string eventType, InviteEntity invite, Guid actorId, DateTimeOffset now, bool? resent = null)
    {
        var payload = new JsonObject { ["email"] = invite.Email, ["role"] = invite.Role };
        if (resent is { } value)
        {
            payload["resent"] = value;
            payload["expiresAt"] = invite.ExpiresAt;
        }

        // No token or hash in events (docs/tech/activity-events.md: never secrets).
        return ActivityEnvelope.Create(
            eventType, now, invite.OrgId, EventActor.User(actorId), new EventEntity(InviteEntity.EntityType, invite.Id, null), null, payload);
    }

    private static Application.Contracts.Invite ToContract(InviteEntity invite, UserRef? inviter, DateTimeOffset now)
        => new(
            invite.Id,
            invite.Email,
            invite.Role,
            invite.StateAt(now) == InviteState.Expired ? "expired" : "pending",
            inviter,
            invite.CreatedAt,
            invite.SentAt,
            invite.ExpiresAt);
}
