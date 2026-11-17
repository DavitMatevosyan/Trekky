using FluentValidation;
using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>The list and board filters (TRK-11.1, TREK-52): known values only, each error under its query parameter.</summary>
internal sealed class WorkItemFilterValidator : AbstractValidator<WorkItemFilter>
{
    public WorkItemFilterValidator()
    {
        RuleFor(f => f.Types)
            .Must(types => types.All(WorkItemTypes.IsKnown)).WithMessage(WorkItemFieldRules.TypeMessage)
            .OverridePropertyName("type");

        RuleFor(f => f.Statuses)
            .Must(statuses => statuses.All(WorkItemStatuses.IsKnown))
            .WithMessage($"Each status filter must be one of: {string.Join(", ", WorkItemStatuses.All)}.")
            .OverridePropertyName("status");

        RuleFor(f => f.Open)
            .Must(open => open is null
                || string.Equals(open, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(open, "false", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Open must be true or false.")
            .OverridePropertyName("open");

        RuleFor(f => f.Assignees)
            .Must(assignees => assignees.Count <= WorkItemFilter.AssigneesMax)
            .WithMessage($"At most {WorkItemFilter.AssigneesMax} assignees can be given.")
            .Must(assignees => assignees.All(a => a is WorkItemFilter.Me or WorkItemFilter.Nobody || Guid.TryParse(a, out _)))
            .WithMessage($"Each assignee must be a user id, {WorkItemFilter.Me} or {WorkItemFilter.Nobody}.")
            .OverridePropertyName("assignee");

        RuleFor(f => f.Parent)
            .Must(parent => parent is null
                || Guid.TryParse(parent, out _)
                || WorkItemKey.TryParse(parent.ToUpperInvariant(), out _, out _))
            .WithMessage("Parent must be a work item key, such as TRK-42, or its id.")
            .OverridePropertyName("parent");

        RuleFor(f => f.Query)
            .Must(query => query is null
                || (query.Length <= WorkItemFilter.QueryMaxLength && !query.Contains('\0', StringComparison.Ordinal)))
            .WithMessage($"The search must be at most {WorkItemFilter.QueryMaxLength} characters of text.")
            .OverridePropertyName("q");

        RuleFor(f => f.CompletedEpics)
            .Must(value => value is null or WorkItemFilter.HideCompletedEpics or WorkItemFilter.ShowCompletedEpics)
            .WithMessage($"Completed epics must be {WorkItemFilter.HideCompletedEpics} or {WorkItemFilter.ShowCompletedEpics}.")
            .OverridePropertyName("completedEpics");
    }
}
