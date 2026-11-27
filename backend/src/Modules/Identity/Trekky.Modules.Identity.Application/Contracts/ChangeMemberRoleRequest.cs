namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>The body of <c>PATCH /api/v1/members/{userId}</c> (TREK-48): the member's new role.</summary>
public sealed record ChangeMemberRoleRequest(string? Role);
