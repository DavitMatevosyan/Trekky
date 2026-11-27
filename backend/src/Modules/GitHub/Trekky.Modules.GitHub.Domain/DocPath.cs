namespace Trekky.Modules.GitHub.Domain;

/// <summary>
/// A file path inside a project's docs folder (TREK-75), as the API and the UI use it: <c>guide/setup.md</c>, never
/// starting with <c>/</c> and never leaving the folder. Requests can only name files under the docs folder, so a reader
/// cannot use the docs endpoints to read the rest of the repository.
/// </summary>
internal static class DocPath
{
    private static readonly string[] MarkdownExtensions = [".md", ".markdown"];

    /// <summary>Image types served through <c>/docs/image</c>, by extension, with their content type.</summary>
    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".svg"] = "image/svg+xml",
    };

    /// <summary>
    /// The path in its one form, or <c>null</c> when it is empty, too long, absolute, has <c>.</c> or <c>..</c>
    /// segments, backslashes or control characters.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > DocFile.PathMaxLength
            || input.Contains('\\', StringComparison.Ordinal) || input.Any(char.IsControl) || input.StartsWith('/'))
        {
            return null;
        }

        var segments = input.Split('/');
        return segments.Any(segment => segment.Length == 0 || segment is "." or "..") ? null : input;
    }

    public static bool IsMarkdown(string path)
        => MarkdownExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    /// <summary>The content type to serve an image with, or <c>null</c> when the file is not an image Trekky serves.</summary>
    public static string? ImageContentType(string path)
    {
        var dot = path.LastIndexOf('.');
        var slash = path.LastIndexOf('/');
        return dot > slash && ImageTypes.TryGetValue(path[dot..], out var type) ? type : null;
    }

    /// <summary>The repository path of a file in the docs folder: <c>docs/</c> + <c>guide/setup.md</c>.</summary>
    public static string InRepository(string docsPath, string path) => docsPath + path;

    /// <summary>
    /// The title shown for a doc: the text of its first level-1 heading (<c># Title</c>), or the file name without its
    /// extension.
    /// </summary>
    public static string TitleOf(string path, string? content)
    {
        if (content is not null)
        {
            var inFence = false;
            foreach (var raw in content.AsSpan().EnumerateLines())
            {
                var line = raw.Trim();
                if (line.StartsWith("```") || line.StartsWith("~~~"))
                {
                    inFence = !inFence;
                    continue;
                }

                if (!inFence && line.Length > 2 && line[0] == '#' && line[1] == ' ')
                {
                    var title = line[2..].Trim().TrimEnd('#').Trim().ToString();
                    if (title.Length > 0)
                    {
                        return title.Length > DocFile.TitleMaxLength ? title[..DocFile.TitleMaxLength] : title;
                    }
                }
            }
        }

        var name = path[(path.LastIndexOf('/') + 1)..];
        var extension = MarkdownExtensions.FirstOrDefault(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase));
        var stem = extension is null ? name : name[..^extension.Length];
        return stem.Length > DocFile.TitleMaxLength ? stem[..DocFile.TitleMaxLength] : stem;
    }
}
