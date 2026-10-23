namespace Trekky.Modules.Identity.Contracts;

/// <summary>Body of <c>POST /api/v1/auth/sign-up</c> (US-01).</summary>
public sealed record SignUpRequest(string? Email, string? Password, string? OrganizationName, string? DisplayName = null);

/// <summary>Body of <c>POST /api/v1/auth/login</c> (US-02).</summary>
public sealed record LoginRequest(string? Email, string? Password);

/// <summary>The current user, organization and role.</summary>
public sealed record MeResponse(UserRef User, OrganizationRef Organization, string Role);

public sealed record UserRef(Guid Id, string DisplayName, string? Email);

public sealed record OrganizationRef(Guid Id, string Name);
