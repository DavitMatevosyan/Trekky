namespace Trekky.Modules.WorkItems.Domain;

/// <summary>
/// The fixed workflow's transitions (US-10 AC2): forward one step, back to any earlier step, and Done reopens to
/// In Progress. Staying in the same status is not a transition.
/// </summary>
internal static class StatusWorkflow
{
    private static readonly Dictionary<string, string[]> Transitions = new(StringComparer.Ordinal)
    {
        [WorkItemStatuses.ToDo] = [WorkItemStatuses.InProgress],
        [WorkItemStatuses.InProgress] = [WorkItemStatuses.ToDo, WorkItemStatuses.InReview],
        [WorkItemStatuses.InReview] = [WorkItemStatuses.ToDo, WorkItemStatuses.InProgress, WorkItemStatuses.Done],
        [WorkItemStatuses.Done] = [WorkItemStatuses.InProgress],
    };

    /// <summary>The statuses an item in <paramref name="from"/> may move to, in workflow order; none for an unknown status.</summary>
    public static IReadOnlyList<string> AllowedTransitions(string from)
        => Transitions.TryGetValue(from, out var targets) ? targets : [];

    public static bool Allows(string from, string to) => AllowedTransitions(from).Contains(to, StringComparer.Ordinal);
}
