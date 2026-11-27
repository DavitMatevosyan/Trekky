namespace Trekky.Modules.GitHub.Application.Docs;

/// <summary>The query string of <c>GET /api/v1/projects/{projectId}/docs/search</c>: <c>q</c>, the words to find.</summary>
internal sealed record SearchDocsQuery(string? Q)
{
    public const int MaxLength = 200;
}
