namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>
/// Body of <c>POST /api/v1/auth/invites/preview</c>: the token from the invite link, sent in the body so it stays out of
/// URLs and logs.
/// </summary>
public sealed record InviteTokenRequest(string? Token);
