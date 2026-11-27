namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>One doc and its Markdown (TREK-75).</summary>
/// <param name="Path">The path inside the docs folder.</param>
/// <param name="Content">The Markdown text; <c>null</c> when the file is larger than 1 MB (<see cref="TooLarge"/>).</param>
/// <param name="BlobSha">GitHub's SHA of this version of the file.</param>
/// <param name="LastCommit">The last commit that changed the file; <c>null</c> when GitHub could not be asked.</param>
/// <param name="HtmlUrl">The file on GitHub.</param>
/// <param name="Stale">GitHub could not be reached just now, so this is the last cached copy.</param>
public sealed record DocContent(
    string Path,
    string Title,
    string? Content,
    string BlobSha,
    int Size,
    bool TooLarge,
    DocCommit? LastCommit,
    string HtmlUrl,
    bool Stale);
