using System.Net.Mail;
using FluentValidation;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Domain;

namespace Trekky.Modules.Identity.Application.Accounts;

/// <summary>Input rules for sign-up (US-01, OpenAPI draft). One error per field, keyed by the JSON field name.</summary>
internal sealed class SignUpRequestValidator : AbstractValidator<SignUpRequest>
{
    public SignUpRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => Trimmed(r.Email))
            .NotEmpty().WithMessage("Email is required.")
            .Must(IsValidEmail).WithMessage("Enter a valid email address.")
            .OverridePropertyName("email");

        RuleFor(r => r.Password ?? string.Empty)
            .Must(password => password.Length >= PasswordPolicy.MinLength)
            .WithMessage($"Password must be at least {PasswordPolicy.MinLength} characters.")
            .Must(password => password.Length <= PasswordPolicy.MaxLength)
            .WithMessage($"Password must be at most {PasswordPolicy.MaxLength} characters.")
            .OverridePropertyName("password");

        RuleFor(r => Trimmed(r.OrganizationName))
            .NotEmpty().WithMessage("Organization name is required.")
            .Must(name => name.Length <= Organization.NameMaxLength)
            .WithMessage($"Organization name must be at most {Organization.NameMaxLength} characters.")
            .OverridePropertyName("organizationName");

        RuleFor(r => Trimmed(r.DisplayName))
            .Must(name => name.Length <= User.DisplayNameMaxLength)
            .WithMessage($"Display name must be at most {User.DisplayNameMaxLength} characters.")
            .OverridePropertyName("displayName");
    }

    private static string Trimmed(string? value) => value?.Trim() ?? string.Empty;

    /// <summary>A single plain address (<c>name@domain</c>), at most 254 characters, no display-name form.</summary>
    public static bool IsValidEmail(string email)
        => email.Length <= User.EmailMaxLength
           && email.IndexOf('@', StringComparison.Ordinal) > 0
           && MailAddress.TryCreate(email, out var address)
           && string.Equals(address.Address, email, StringComparison.Ordinal);
}
