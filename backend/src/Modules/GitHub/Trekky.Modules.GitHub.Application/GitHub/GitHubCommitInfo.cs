namespace Trekky.Modules.GitHub.Application.GitHub;

/// <summary>A commit as the docs viewer shows it: the first line of its message, its author's name and date.</summary>
internal sealed record GitHubCommitInfo(string Sha, string TreeSha, string Message, string Author, DateTimeOffset CommittedAt);
