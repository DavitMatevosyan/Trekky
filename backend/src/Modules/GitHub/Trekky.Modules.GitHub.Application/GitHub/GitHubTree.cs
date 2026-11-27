namespace Trekky.Modules.GitHub.Application.GitHub;

/// <summary>A git tree read recursively; <see cref="Truncated"/> when GitHub cut it short (over 100,000 entries or 7 MB).</summary>
internal sealed record GitHubTree(IReadOnlyList<GitHubTreeEntry> Entries, bool Truncated);
