namespace Trekky.Modules.WorkItems.Domain;

/// <summary>The fixed workflow's statuses (US-10 AC1), in workflow order; <see cref="StatusWorkflow"/> has the transitions.</summary>
internal static class WorkItemStatuses
{
    public const string ToDo = "todo";

    public const string InProgress = "in_progress";

    public const string InReview = "in_review";

    public const string Done = "done";

    public static readonly IReadOnlyList<string> All = [ToDo, InProgress, InReview, Done];

    public static bool IsKnown(string? status) => status is not null && All.Contains(status, StringComparer.Ordinal);
}
