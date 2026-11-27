using FluentValidation;

namespace Trekky.Modules.GitHub.Application.Docs;

/// <summary>A search needs some text, and not a page of it.</summary>
internal sealed class SearchDocsQueryValidator : AbstractValidator<SearchDocsQuery>
{
    public SearchDocsQueryValidator()
    {
        RuleFor(q => q.Q)
            .Must(q => !string.IsNullOrWhiteSpace(q)).WithMessage("Type something to search for.")
            .MaximumLength(SearchDocsQuery.MaxLength).WithMessage($"Search for at most {SearchDocsQuery.MaxLength} characters.")
            .OverridePropertyName("q");
    }
}
