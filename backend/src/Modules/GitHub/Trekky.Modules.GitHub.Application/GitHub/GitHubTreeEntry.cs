namespace Trekky.Modules.GitHub.Application.GitHub;

/// <summary>
/// A file or folder in a git tree: its path relative to the tree, <c>blob</c> or <c>tree</c>, its SHA and, for a
/// blob, its size in bytes.
/// </summary>
internal sealed record GitHubTreeEntry(string Path, string Type, string Sha, long Size);
