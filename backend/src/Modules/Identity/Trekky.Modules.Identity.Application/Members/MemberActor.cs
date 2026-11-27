namespace Trekky.Modules.Identity.Application.Members;

/// <summary>
/// The caller changing or removing a member, by user id. Their role is not taken from the token: the service reads the
/// role they have now, under the organization's lock (ADR-0034).
/// </summary>
internal sealed record MemberActor(Guid UserId);
