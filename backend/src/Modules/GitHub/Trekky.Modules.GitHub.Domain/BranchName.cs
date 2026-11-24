namespace Trekky.Modules.GitHub.Domain;

/// <summary>Git's rules for a branch name (<c>git check-ref-format --branch</c>), so a bad one is refused before GitHub is asked.</summary>
internal static class BranchName
{
    public const int MaxLength = 255;

    private static readonly char[] Forbidden = [' ', '~', '^', ':', '?', '*', '[', '\\'];

    public static bool IsValid(string? name)
        => name is { Length: > 0 and <= MaxLength }
            && name != "@"
            && !name.Any(c => char.IsControl(c) || Forbidden.Contains(c))
            && !name.Contains("..", StringComparison.Ordinal)
            && !name.Contains("@{", StringComparison.Ordinal)
            && !name.Contains("//", StringComparison.Ordinal)
            && !name.StartsWith('/') && !name.EndsWith('/')
            && !name.StartsWith('-')
            && !name.EndsWith('.')
            && !name.EndsWith(".lock", StringComparison.Ordinal)
            && name.Split('/').All(segment => !segment.StartsWith('.'));
}
