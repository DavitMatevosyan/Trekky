namespace Trekky.Modules.GitHub.Domain;

/// <summary>
/// The docs folder inside a repository, kept in one form: segments joined by <c>/</c> with a trailing <c>/</c>
/// (<c>docs/</c>, <c>handbook/en/</c>), or empty for the repository root. Users may type <c>/docs</c> or <c>docs</c>.
/// </summary>
internal static class DocsPath
{
    public const int MaxLength = 200;

    /// <summary>The normalized folder, or <c>null</c> when the input is not a safe relative folder path.</summary>
    public static string? Normalize(string? input)
    {
        if (input is null)
        {
            return null;
        }

        var trimmed = input.Trim();
        if (trimmed.Length > MaxLength || trimmed.Contains('\\', StringComparison.Ordinal) || trimmed.Any(char.IsControl))
        {
            return null;
        }

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".." || segment.Trim().Length != segment.Length))
        {
            return null;
        }

        // "//" inside the path would be read as an empty segment by GitHub's API, so it is refused rather than fixed.
        if (trimmed.Trim('/').Contains("//", StringComparison.Ordinal))
        {
            return null;
        }

        return segments.Length == 0 ? string.Empty : string.Join('/', segments) + "/";
    }
}
