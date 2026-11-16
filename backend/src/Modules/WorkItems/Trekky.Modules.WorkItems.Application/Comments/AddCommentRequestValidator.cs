using FluentValidation;
using Trekky.Modules.WorkItems.Application.Contracts;

namespace Trekky.Modules.WorkItems.Application.Comments;

/// <summary>US-12 AC1: a Markdown body that says something, up to 20,000 characters.</summary>
internal sealed class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
        => RuleFor(r => r.BodyMd).ValidCommentBody().OverridePropertyName("bodyMd");
}
