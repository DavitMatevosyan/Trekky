namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>Body of <c>POST /api/v1/auth/sign-up</c> (US-01).</summary>
public sealed record SignUpRequest(string? Email, string? Password, string? OrganizationName, string? DisplayName = null);
