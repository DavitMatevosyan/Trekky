namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>A doc that matches a search, with a snippet of its text around the matches (TREK-75).</summary>
public sealed record DocSearchHit(string Path, string Title, IReadOnlyList<DocSnippetPart> Snippet);
