namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>A member of the caller's organization with their role (US-13: the assignee picker).</summary>
public sealed record Member(UserRef User, string Role);
