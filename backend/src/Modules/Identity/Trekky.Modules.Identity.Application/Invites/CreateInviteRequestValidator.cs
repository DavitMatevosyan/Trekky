using FluentValidation;
using Trekky.Modules.Identity.Application.Accounts;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Domain;

namespace Trekky.Modules.Identity.Application.Invites;

/// <summary>Input rules for <c>POST /api/v1/invites</c> (TREK-47). Who may invite which role is checked by the service.</summary>
internal sealed class CreateInviteRequestValidator : AbstractValidator<CreateInviteRequest>
{
    public CreateInviteRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Email == null ? string.Empty : r.Email.Trim())
            .NotEmpty().WithMessage("Email is required.")
            .Must(SignUpRequestValidator.IsValidEmail).WithMessage("Enter a valid email address.")
            .OverridePropertyName("email");

        RuleFor(r => r.Role)
            .Must(InviteRoles.IsInvitable)
            .WithMessage($"Role must be one of: {string.Join(", ", InviteRoles.Invitable)}.")
            .OverridePropertyName("role");
    }
}
