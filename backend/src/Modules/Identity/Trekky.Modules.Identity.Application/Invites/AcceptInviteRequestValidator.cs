using FluentValidation;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Domain;

namespace Trekky.Modules.Identity.Application.Invites;

/// <summary>Input rules for accepting an invite: the same password and display-name rules as sign-up (US-01).</summary>
internal sealed class AcceptInviteRequestValidator : AbstractValidator<AcceptInviteRequest>
{
    public AcceptInviteRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Token)
            .Must(token => !string.IsNullOrEmpty(token)).WithMessage("The invite link is incomplete.")
            .OverridePropertyName("token");

        RuleFor(r => r.Password ?? string.Empty)
            .Must(password => password.Length >= PasswordPolicy.MinLength)
            .WithMessage($"Password must be at least {PasswordPolicy.MinLength} characters.")
            .Must(password => password.Length <= PasswordPolicy.MaxLength)
            .WithMessage($"Password must be at most {PasswordPolicy.MaxLength} characters.")
            .OverridePropertyName("password");

        RuleFor(r => r.DisplayName == null ? string.Empty : r.DisplayName.Trim())
            .Must(name => name.Length <= User.DisplayNameMaxLength)
            .WithMessage($"Display name must be at most {User.DisplayNameMaxLength} characters.")
            .OverridePropertyName("displayName");
    }
}
