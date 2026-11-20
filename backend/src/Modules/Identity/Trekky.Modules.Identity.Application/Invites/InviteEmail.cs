namespace Trekky.Modules.Identity.Application.Invites;

/// <summary>The content of one invite email.</summary>
internal sealed record InviteEmail(
    string To,
    string OrganizationName,
    string InviterName,
    string Role,
    string AcceptUrl,
    DateTimeOffset ExpiresAt);
