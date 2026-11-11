using FluentValidation;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.Modules.WorkItems.Application.WorkItems;
using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Application.Comments;

/// <summary>US-12 AC1: a Markdown body that says something, up to 20,000 characters.</summary>
internal sealed class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public static readonly string BodyRequiredMessage = "Write something to comment.";

    public static readonly string BodyTooLongMessage = $"A comment can be at most {WorkItemComment.BodyMaxLength:N0} characters.";

    public const string NullCharacterMessage = "The comment contains a character that is not allowed.";

    public AddCommentRequestValidator()
        => RuleFor(r => r.BodyMd)
            .Must(body => !string.IsNullOrWhiteSpace(body))
            .WithMessage(BodyRequiredMessage)
            .MaximumLength(WorkItemComment.BodyMaxLength)
            .WithMessage(BodyTooLongMessage)
            .Must(WorkItemFieldRules.NoNullCharacter)
            .WithMessage(NullCharacterMessage)
            .OverridePropertyName("bodyMd");
}
