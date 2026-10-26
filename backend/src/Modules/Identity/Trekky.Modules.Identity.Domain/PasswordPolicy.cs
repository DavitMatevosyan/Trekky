namespace Trekky.Modules.Identity.Domain;

/// <summary>Password length limits (US-01): enforced by sign-up validation and by Identity's own password options.</summary>
internal static class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;
}
