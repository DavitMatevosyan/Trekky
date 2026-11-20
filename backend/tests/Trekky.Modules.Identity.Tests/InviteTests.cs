using Trekky.Modules.Identity.Domain;

namespace Trekky.Modules.Identity.Tests;

/// <summary>TREK-47: an invite's link, expiry and life cycle (ADR-0027).</summary>
public sealed class InviteTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_invite_is_pending_for_seven_days_and_stores_only_the_tokens_hash()
    {
        var (invite, token) = NewInvite();

        Assert.Equal(InviteState.Pending, invite.StateAt(Now));
        Assert.Equal(Now + TimeSpan.FromDays(7), invite.ExpiresAt);
        Assert.Equal(Now, invite.SentAt);
        Assert.Equal(InviteToken.Hash(token), invite.TokenHash);
        Assert.Equal("ADA@EXAMPLE.COM", invite.NormalizedEmail);
        Assert.True(InviteToken.IsWellFormed(token));
    }

    [Fact]
    public void An_invite_expires_at_its_expiry_time()
    {
        var (invite, _) = NewInvite();

        Assert.Equal(InviteState.Pending, invite.StateAt(invite.ExpiresAt.AddTicks(-1)));
        Assert.Equal(InviteState.Expired, invite.StateAt(invite.ExpiresAt));
        Assert.True(invite.IsOpen);
    }

    [Fact]
    public void Resending_replaces_the_token_and_restarts_the_expiry()
    {
        var (invite, first) = NewInvite();
        var later = Now.AddDays(10);

        var second = invite.Resend(later);

        Assert.NotEqual(first, second);
        Assert.Equal(InviteToken.Hash(second), invite.TokenHash);
        Assert.Equal(later + Invite.Lifetime, invite.ExpiresAt);
        Assert.Equal(InviteState.Pending, invite.StateAt(later));
    }

    [Fact]
    public void Accepting_records_the_new_member_and_closes_the_invite()
    {
        var (invite, _) = NewInvite();
        var userId = Guid.NewGuid();

        invite.Accept(userId, Now.AddHours(1));

        Assert.Equal(InviteState.Accepted, invite.StateAt(Now.AddHours(1)));
        Assert.Equal(userId, invite.AcceptedUserId);
        Assert.False(invite.IsOpen);
        Assert.Throws<InvalidOperationException>(() => invite.Resend(Now));
        Assert.Throws<InvalidOperationException>(() => invite.Revoke(Now));
    }

    [Fact]
    public void A_revoked_or_expired_invite_cannot_be_accepted()
    {
        var (revoked, _) = NewInvite();
        revoked.Revoke(Now);
        var (expired, _) = NewInvite();

        Assert.Equal(InviteState.Revoked, revoked.StateAt(Now));
        Assert.Throws<InvalidOperationException>(() => revoked.Accept(Guid.NewGuid(), Now));
        Assert.Throws<InvalidOperationException>(() => expired.Accept(Guid.NewGuid(), expired.ExpiresAt));
    }

    private static (Invite Invite, string Token) NewInvite()
        => Invite.Create(Guid.NewGuid(), "Ada@Example.com", Roles.Member, Guid.NewGuid(), Now);
}
