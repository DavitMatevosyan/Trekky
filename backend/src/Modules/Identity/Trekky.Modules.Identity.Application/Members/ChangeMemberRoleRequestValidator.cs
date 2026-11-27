using FluentValidation;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Domain;

namespace Trekky.Modules.Identity.Application.Members;

/// <summary>Input rules for <c>PATCH /api/v1/members/{userId}</c> (TREK-48). Who may give which role is checked by the service.</summary>
internal sealed class ChangeMemberRoleRequestValidator : AbstractValidator<ChangeMemberRoleRequest>
{
    public ChangeMemberRoleRequestValidator()
    {
        RuleFor(r => r.Role)
            .Must(MemberRules.IsRole)
            .WithMessage($"Role must be one of: {string.Join(", ", MemberRules.Assignable)}.")
            .OverridePropertyName("role");
    }
}
