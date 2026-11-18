using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>
/// Narrows a project's list or board (TRK-11.1, TREK-52). Parameters combine with AND and the values of one parameter
/// with OR; an empty list means no filter on it. The values are as the query string gives them and are checked by
/// <see cref="WorkItemFilterValidator"/>; the computed properties read them once they are valid.
/// </summary>
internal sealed record WorkItemFilter
{
    public const int QueryMaxLength = 200;

    public const int AssigneesMax = 50;

    /// <summary>The assignee value for the caller.</summary>
    public const string Me = "me";

    /// <summary>The assignee value for items assigned to nobody.</summary>
    public const string Nobody = "none";

    public const string HideCompletedEpics = "hide";

    public const string ShowCompletedEpics = "show";

    /// <summary>Only these types (TRK-11.1).</summary>
    public IReadOnlyList<string> Types { get; init; } = [];

    /// <summary>Only these statuses.</summary>
    public IReadOnlyList<string> Statuses { get; init; } = [];

    /// <summary><c>true</c>: only items whose own status is not Done; <c>false</c> or nothing: no filter.</summary>
    public string? Open { get; init; }

    /// <summary>Only items assigned to one of these: user ids, <see cref="Me"/> or <see cref="Nobody"/>.</summary>
    public IReadOnlyList<string> Assignees { get; init; } = [];

    /// <summary>Only items under this item, at any depth (an Epic's stories, tasks and subtasks): its key or its id.</summary>
    public string? Parent { get; init; }

    /// <summary>Only items whose key starts with or whose title contains this, case-insensitive.</summary>
    public string? Query { get; init; }

    /// <summary>
    /// <see cref="HideCompletedEpics"/> or nothing (the default): leave out every Epic in Done and everything under it,
    /// whatever its own status. <see cref="ShowCompletedEpics"/>: keep them.
    /// </summary>
    public string? CompletedEpics { get; init; }

    /// <summary>The caller, whom <see cref="Me"/> stands for.</summary>
    public Guid CurrentUserId { get; init; }

    public bool OnlyOpen => string.Equals(Open, "true", StringComparison.OrdinalIgnoreCase);

    public bool KeepsCompletedEpics => CompletedEpics == ShowCompletedEpics;

    public bool IncludesUnassigned => Assignees.Contains(Nobody, StringComparer.Ordinal);

    /// <summary>The people in <see cref="Assignees"/>, with <see cref="Me"/> read as <see cref="CurrentUserId"/>.</summary>
    public IReadOnlyList<Guid> AssigneeIds => Assignees
        .Select(a => a == Me ? CurrentUserId : Guid.TryParse(a, out var id) ? id : (Guid?)null)
        .OfType<Guid>()
        .Distinct()
        .ToList();

    /// <summary>The parent as an id, when it is given as one.</summary>
    public Guid? ParentId => Guid.TryParse(Parent, out var id) ? id : null;

    /// <summary>The filter as the query string gives it: each value once, blanks dropped, the search and parent trimmed.</summary>
    public static WorkItemFilter From(
        string[]? types = null,
        string? query = null,
        string[]? statuses = null,
        string? open = null,
        string[]? assignees = null,
        string? parent = null,
        string? completedEpics = null,
        Guid currentUserId = default)
        => new()
        {
            Types = Distinct(types),
            Query = Blank(query)?.Trim(),
            Statuses = Distinct(statuses),
            Open = Blank(open),
            Assignees = Distinct(assignees),
            Parent = Blank(parent)?.Trim(),
            CompletedEpics = Blank(completedEpics),
            CurrentUserId = currentUserId,
        };

    /// <summary>The parent as a key, in any case (<c>trk-42</c> is <c>TRK-42</c>), when it is given as one.</summary>
    public bool TryGetParentKey(out string projectKey, out int number)
    {
        projectKey = string.Empty;
        number = 0;
        return ParentId is null && Parent is not null && WorkItemKey.TryParse(Parent.ToUpperInvariant(), out projectKey, out number);
    }

    private static List<string> Distinct(string[]? values)
        => (values ?? []).Where(v => !string.IsNullOrEmpty(v)).Distinct(StringComparer.Ordinal).ToList();

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
