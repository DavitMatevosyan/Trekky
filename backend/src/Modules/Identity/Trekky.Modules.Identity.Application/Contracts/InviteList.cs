namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>The organization's open invites, newest first (TREK-47).</summary>
public sealed record InviteList(IReadOnlyList<Invite> Items);
