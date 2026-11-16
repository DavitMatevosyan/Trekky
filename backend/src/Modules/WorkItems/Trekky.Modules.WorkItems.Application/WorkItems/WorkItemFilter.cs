namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>
/// Narrows a project's list, e.g. to the candidates for a parent (TRK-11.1): only these <see cref="Types"/> (all when
/// empty), and only items whose key starts with or whose title contains <see cref="Query"/> (case-insensitive).
/// Checked by <see cref="WorkItemFilterValidator"/>.
/// </summary>
internal sealed record WorkItemFilter(IReadOnlyList<string> Types, string? Query)
{
    public const int QueryMaxLength = 200;

    /// <summary>The filter as the query string gives it: each type once, the search trimmed and <c>null</c> when blank.</summary>
    public static WorkItemFilter From(string[]? types, string? query)
        => new(
            (types ?? []).Where(t => !string.IsNullOrEmpty(t)).Distinct(StringComparer.Ordinal).ToList(),
            string.IsNullOrWhiteSpace(query) ? null : query.Trim());
}
