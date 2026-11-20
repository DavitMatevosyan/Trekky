namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>
/// Body of <c>POST /api/v1/auth/invites/accept</c> (TREK-47): the token from the link and the new account's password and
/// optional display name. The email is the invite's; it cannot be changed here.
/// </summary>
public sealed record AcceptInviteRequest(string? Token, string? Password, string? DisplayName = null);
