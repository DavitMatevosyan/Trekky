namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>Body of <c>POST /api/v1/auth/login</c> (US-02).</summary>
public sealed record LoginRequest(string? Email, string? Password);
