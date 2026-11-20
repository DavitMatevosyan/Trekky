namespace Trekky.Modules.Identity.Application.Invites;

/// <summary>The caller managing invites: their user id and their role from the access token.</summary>
internal sealed record InviteActor(Guid UserId, string? Role);
