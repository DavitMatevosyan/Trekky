using FluentValidation;
using Trekky.Modules.WorkItems.Application.WorkItems;
using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Application.Comments;

/// <summary>What a comment's body may be (US-12), for new and edited comments alike.</summary>
internal static class CommentRules
{
    public const string BodyRequiredMessage = "Write something to comment.";

    public const string NullCharacterMessage = "The comment contains a character that is not allowed.";

    public static readonly string BodyTooLongMessage = $"A comment can be at most {WorkItemComment.BodyMaxLength:N0} characters.";

    /// <summary>Markdown that says something, up to 20,000 characters, without NUL (Postgres text cannot hold it).</summary>
    public static IRuleBuilderOptions<T, string?> ValidCommentBody<T>(this IRuleBuilder<T, string?> rule)
        => rule
            .Must(body => !string.IsNullOrWhiteSpace(body))
            .WithMessage(BodyRequiredMessage)
            .MaximumLength(WorkItemComment.BodyMaxLength)
            .WithMessage(BodyTooLongMessage)
            .Must(WorkItemFieldRules.NoNullCharacter)
            .WithMessage(NullCharacterMessage);
}
