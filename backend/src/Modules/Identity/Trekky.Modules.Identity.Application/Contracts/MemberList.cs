namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>Every member of the caller's organization, by display name. Small until invites (v1.1), so not paged.</summary>
public sealed record MemberList(IReadOnlyList<Member> Items);
