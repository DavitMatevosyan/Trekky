namespace Trekky.Modules.Projects.Application.Contracts;

/// <summary>A page of projects ordered by name; pass <see cref="NextCursor"/> back as <c>cursor</c> for the next one.</summary>
public sealed record ProjectPage(IReadOnlyList<Project> Items, string? NextCursor);
