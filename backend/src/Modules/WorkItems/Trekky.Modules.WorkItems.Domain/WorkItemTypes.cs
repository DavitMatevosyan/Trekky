namespace Trekky.Modules.WorkItems.Domain;

/// <summary>The six work item types (US-06 AC1), as stored and sent over the API.</summary>
internal static class WorkItemTypes
{
    public const string Epic = "epic";

    public const string Story = "story";

    public const string Task = "task";

    public const string SubTask = "subtask";

    public const string Bug = "bug";

    public const string TechDebt = "tech_debt";

    /// <summary>Every type, from the top of the hierarchy down.</summary>
    public static readonly IReadOnlyList<string> All = [Epic, Story, Task, SubTask, Bug, TechDebt];

    public static bool IsKnown(string? type) => type is not null && All.Contains(type, StringComparer.Ordinal);
}
