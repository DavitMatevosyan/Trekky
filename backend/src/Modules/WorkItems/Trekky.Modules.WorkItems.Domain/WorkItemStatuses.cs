namespace Trekky.Modules.WorkItems.Domain;

/// <summary>The fixed workflow's statuses (US-10 AC1). Transitions between them arrive with TRK-12.</summary>
internal static class WorkItemStatuses
{
    public const string ToDo = "todo";

    public const string InProgress = "in_progress";

    public const string InReview = "in_review";

    public const string Done = "done";

    public static readonly IReadOnlyList<string> All = [ToDo, InProgress, InReview, Done];
}
