using FluentValidation;
using Trekky.Modules.Identity.Application.Contracts;

namespace Trekky.Modules.Identity.Application.Accounts;

/// <summary>Login needs both fields (US-02); the credentials themselves are checked by Identity.</summary>
internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email)
            .Must(email => !string.IsNullOrWhiteSpace(email)).WithMessage("Email is required.")
            .OverridePropertyName("email");

        RuleFor(r => r.Password)
            .Must(password => !string.IsNullOrEmpty(password)).WithMessage("Password is required.")
            .OverridePropertyName("password");
    }
}
