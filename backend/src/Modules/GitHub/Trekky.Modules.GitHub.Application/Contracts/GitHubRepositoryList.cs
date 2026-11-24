namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>The repositories an installation can reach, by name.</summary>
/// <param name="Truncated">More repositories exist than were listed; type the name instead of picking it.</param>
public sealed record GitHubRepositoryList(IReadOnlyList<GitHubRepository> Items, bool Truncated);
