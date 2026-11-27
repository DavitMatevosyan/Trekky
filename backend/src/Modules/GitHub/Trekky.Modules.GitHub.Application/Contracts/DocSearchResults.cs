namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>The best matches for a search across a project's docs, at most 20, best first (TREK-75).</summary>
/// <param name="Incomplete">Some docs are not cached yet (or are too large), so they could not be searched.</param>
public sealed record DocSearchResults(string Query, IReadOnlyList<DocSearchHit> Results, bool Incomplete);
