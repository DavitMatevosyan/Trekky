using FluentValidation;
using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>The list filters (TRK-11.1): known types only, and a search of at most 200 characters of text.</summary>
internal sealed class WorkItemFilterValidator : AbstractValidator<WorkItemFilter>
{
    public WorkItemFilterValidator()
    {
        RuleFor(f => f.Types)
            .Must(types => types.All(WorkItemTypes.IsKnown)).WithMessage(WorkItemFieldRules.TypeMessage)
            .OverridePropertyName("type");

        RuleFor(f => f.Query)
            .Must(query => query is null
                || (query.Length <= WorkItemFilter.QueryMaxLength && !query.Contains('\0', StringComparison.Ordinal)))
            .WithMessage($"The search must be at most {WorkItemFilter.QueryMaxLength} characters of text.")
            .OverridePropertyName("q");
    }
}
