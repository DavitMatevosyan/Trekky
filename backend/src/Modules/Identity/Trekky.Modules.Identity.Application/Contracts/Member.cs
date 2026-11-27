namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>
/// A member of the caller's organization (US-13: the assignee picker; TREK-48: the members page): their role, whether they
/// are a person or a bot (<see cref="MemberKinds"/>), and the ETag to send in <c>If-Match</c> when changing their role.
/// </summary>
public sealed record Member(UserRef User, string Role, string Kind, string Etag);
