namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>
/// An open invite of the caller's organization (TREK-47). <c>Status</c> is <c>pending</c> or <c>expired</c>; an expired
/// invite can be resent. <c>InvitedBy</c> is <c>null</c> once the inviter has left the organization.
/// </summary>
public sealed record Invite(
    Guid Id,
    string Email,
    string Role,
    string Status,
    UserRef? InvitedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset SentAt,
    DateTimeOffset ExpiresAt);
