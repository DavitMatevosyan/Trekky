namespace Trekky.Modules.GitHub.Application.Repositories;

/// <summary>The outcome and, when saved (or stale), the project's link.</summary>
internal sealed record LinkRepositoryResult(LinkRepositoryOutcome Outcome, VersionedProjectRepositoryLink? Link = null);
