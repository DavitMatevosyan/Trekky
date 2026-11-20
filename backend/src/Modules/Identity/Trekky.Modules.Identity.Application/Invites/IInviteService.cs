using Trekky.Modules.Identity.Application.Contracts;

namespace Trekky.Modules.Identity.Application.Invites;

/// <summary>Invites to the caller's organization and accepting them (TREK-47, ADR-0027). Implemented in Infrastructure.</summary>
internal interface IInviteService
{
    /// <summary>The organization's open invites (pending or expired), newest first.</summary>
    Task<InviteList> ListAsync(CancellationToken cancellationToken);

    /// <summary>Creates an invite with a new link and emails it; <paramref name="actor"/> is the caller.</summary>
    Task<(CreateInviteOutcome Outcome, SentInvite? Sent)> CreateAsync(
        string email, string role, InviteActor actor, CancellationToken cancellationToken);

    /// <summary>Replaces the invite's link (the old one stops working), restarts its expiry and emails it again.</summary>
    Task<(InviteChangeOutcome Outcome, SentInvite? Sent)> ResendAsync(
        Guid inviteId, InviteActor actor, CancellationToken cancellationToken);

    /// <summary>Withdraws an open invite; its link then says that it was withdrawn.</summary>
    Task<InviteChangeOutcome> RevokeAsync(Guid inviteId, InviteActor actor, CancellationToken cancellationToken);

    /// <summary>What the link is for, read without a tenant (policy <c>invite_token_read</c>).</summary>
    Task<(InviteLinkOutcome Outcome, InvitePreview? Preview)> PreviewAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the account with the invite's email, makes it a member of the inviter's organization with the invited
    /// role, marks the invite accepted, then signs the new user in (session cookie, as after sign-up).
    /// </summary>
    Task<(InviteLinkOutcome Outcome, Me? Me)> AcceptAsync(
        string token, string password, string? displayName, CancellationToken cancellationToken);
}
