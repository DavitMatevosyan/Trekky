namespace Trekky.Modules.Identity.Domain;

/// <summary>
/// An invitation to join an organization with a role (TREK-47, ADR-0027). The emailed link carries a random token;
/// only its SHA-256 hash is stored. Single use: accepting, revoking or resending (a new token) ends the old link.
/// </summary>
internal sealed class Invite
{
    public const string EntityType = "invite";

    /// <summary>How long a link works after it is sent or resent.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public Guid Id { get; init; }

    public Guid OrgId { get; init; }

    /// <summary>The address as the inviter typed it (trimmed); the account is created with it.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Upper-cased like Identity's <c>NormalizedEmail</c>: one pending invite per address per organization.</summary>
    public string NormalizedEmail { get; init; } = string.Empty;

    public string Role { get; init; } = Roles.Member;

    public byte[] TokenHash { get; private set; } = [];

    public Guid InvitedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the current link was made (first send or the last resend).</summary>
    public DateTimeOffset SentAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public Guid? AcceptedUserId { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// Postgres <c>xmin</c>: accepting, resending and revoking each check it, so two of them racing on one invite
    /// cannot both win (a withdrawn invite never also lets someone in).
    /// </summary>
    public uint Version { get; init; }

    /// <summary>A new invite with a fresh link; returns the token for the link (never stored).</summary>
    public static (Invite Invite, string Token) Create(
        Guid orgId, string email, string role, Guid invitedBy, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var invite = new Invite
        {
            Id = Guid.CreateVersion7(now),
            OrgId = orgId,
            Email = email,
            NormalizedEmail = Normalize(email),
            Role = role,
            InvitedBy = invitedBy,
            CreatedAt = now,
        };
        return (invite, invite.NewLink(now));
    }

    /// <summary>Identity's normalization (upper invariant), so invites and user accounts compare the same way.</summary>
    public static string Normalize(string email) => email.Trim().ToUpperInvariant();

    public InviteState StateAt(DateTimeOffset now)
        => AcceptedAt is not null ? InviteState.Accepted
            : RevokedAt is not null ? InviteState.Revoked
            : now >= ExpiresAt ? InviteState.Expired
            : InviteState.Pending;

    /// <summary>Not yet accepted or revoked; an expired invite is still open and can be resent.</summary>
    public bool IsOpen => AcceptedAt is null && RevokedAt is null;

    /// <summary>Replaces the link (the old one stops working) and restarts the expiry; returns the new token.</summary>
    public string Resend(DateTimeOffset now)
    {
        EnsureOpen();
        return NewLink(now);
    }

    public void Revoke(DateTimeOffset now)
    {
        EnsureOpen();
        RevokedAt = now;
    }

    public void Accept(Guid userId, DateTimeOffset now)
    {
        if (StateAt(now) != InviteState.Pending)
        {
            throw new InvalidOperationException($"Only a pending invite can be accepted; this one is {StateAt(now)}.");
        }

        AcceptedAt = now;
        AcceptedUserId = userId;
    }

    private string NewLink(DateTimeOffset now)
    {
        var token = InviteToken.New();
        TokenHash = InviteToken.Hash(token);
        SentAt = now;
        ExpiresAt = now + Lifetime;
        return token;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("The invite was already accepted or revoked.");
        }
    }
}
