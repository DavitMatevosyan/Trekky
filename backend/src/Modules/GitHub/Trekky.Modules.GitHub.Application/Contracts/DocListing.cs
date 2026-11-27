namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>A Markdown file in the docs folder (TREK-75).</summary>
/// <param name="Path">The path inside the docs folder, e.g. <c>adr/0001-modular-monolith.md</c>.</param>
/// <param name="Title">The doc's first <c>#</c> heading, or its file name without the extension.</param>
/// <param name="TooLarge">Larger than 1 MB: listed and linked to GitHub, but not shown or searched in Trekky.</param>
public sealed record DocListing(string Path, string Title, int Size, bool TooLarge);
