namespace Trekky.Modules.Projects.Application.Contracts;

/// <summary>Body of <c>POST /api/v1/projects</c> (US-05).</summary>
public sealed record CreateProjectRequest(string? Name, string? Key);
