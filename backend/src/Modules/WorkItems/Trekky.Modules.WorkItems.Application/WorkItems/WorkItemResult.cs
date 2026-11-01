namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>
/// The result of creating or editing an item: the item (also the latest version when <see cref="WorkItemOutcome.Stale"/>),
/// or the rule that was broken.
/// </summary>
internal sealed record WorkItemResult(WorkItemOutcome Outcome, VersionedWorkItem? Item = null, RuleViolation? Violation = null)
{
    public static readonly WorkItemResult NotFound = new(WorkItemOutcome.NotFound);
}
