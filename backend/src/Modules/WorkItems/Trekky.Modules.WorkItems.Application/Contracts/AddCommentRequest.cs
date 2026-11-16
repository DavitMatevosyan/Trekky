namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>Body of <c>POST /api/v1/work-items/{workItemId}/comments</c> (US-12).</summary>
/// <param name="BodyMd">Markdown, 1–20,000 characters and not only whitespace.</param>
public sealed record AddCommentRequest(string? BodyMd);
