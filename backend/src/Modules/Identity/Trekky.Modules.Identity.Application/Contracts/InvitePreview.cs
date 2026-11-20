namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>What the invite page shows before someone accepts (TREK-47): the address, role, organization and inviter.</summary>
public sealed record InvitePreview(
    string Email,
    string Role,
    OrganizationRef Organization,
    string? InvitedBy,
    DateTimeOffset ExpiresAt);
