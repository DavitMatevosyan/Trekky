namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>Body of <c>POST /api/v1/invites</c> (TREK-47): who to invite and with which role (admin, member or viewer).</summary>
public sealed record CreateInviteRequest(string? Email, string? Role);
