using FluentValidation;
using Trekky.Modules.WorkItems.Application.Contracts;

namespace Trekky.Modules.WorkItems.Application.Comments;

/// <summary>US-12 AC2: an edited comment follows the same rules as a new one.</summary>
internal sealed class EditCommentRequestValidator : AbstractValidator<EditCommentRequest>
{
    public EditCommentRequestValidator()
        => RuleFor(r => r.BodyMd).ValidCommentBody().OverridePropertyName("bodyMd");
}
