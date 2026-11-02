using FluentValidation;
using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>Field rules shared by create and edit (US-06 AC1), so both answer with the same messages.</summary>
internal static class WorkItemFieldRules
{
    public static readonly string TypeMessage = $"Type must be one of: {string.Join(", ", WorkItemTypes.All)}.";

    public static IRuleBuilderOptions<T, string?> ValidType<T>(this IRuleBuilder<T, string?> rule)
        => rule.Must(WorkItemTypes.IsKnown).WithMessage(TypeMessage);

    public static IRuleBuilderOptions<T, string?> ValidTitle<T>(this IRuleBuilder<T, string?> rule)
        => rule
            .Must(title => !string.IsNullOrWhiteSpace(title)).WithMessage("Title is required.")
            .Must(title => title is null || title.Trim().Length <= WorkItem.TitleMaxLength)
            .WithMessage($"Title must be at most {WorkItem.TitleMaxLength} characters.")
            .Must(NoNullCharacter).WithMessage("Title contains a character that is not allowed.");

    public static IRuleBuilderOptions<T, string?> ValidDescription<T>(this IRuleBuilder<T, string?> rule)
        => rule
            .Must(description => description is null || description.Length <= WorkItem.DescriptionMaxLength)
            .WithMessage($"Description must be at most {WorkItem.DescriptionMaxLength} characters.")
            .Must(NoNullCharacter).WithMessage("Description contains a character that is not allowed.");

    /// <summary>The title as stored: trimmed.</summary>
    public static string NormalizeTitle(string title) => title.Trim();

    /// <summary>The description as stored: <c>null</c> when it is empty or only whitespace.</summary>
    public static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description;

    // Postgres text cannot hold NUL; without this check it would fail the insert with a 500.
    private static bool NoNullCharacter(string? value) => value is null || !value.Contains('\0', StringComparison.Ordinal);
}
