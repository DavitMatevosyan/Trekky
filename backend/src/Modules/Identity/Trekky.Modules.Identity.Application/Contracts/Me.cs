namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>The current user, organization and role.</summary>
public sealed record Me(UserRef User, OrganizationRef Organization, string Role);
