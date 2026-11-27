namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>A run of a search snippet's text; <see cref="Match"/> when it is a word the search matched.</summary>
public sealed record DocSnippetPart(string Text, bool Match);
