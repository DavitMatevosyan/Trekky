namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>Body of <c>PATCH /api/v1/comments/{commentId}</c> (US-12 AC2), sent as <c>application/merge-patch+json</c>.</summary>
/// <param name="BodyMd">The new Markdown, with the same rules as a new comment.</param>
public sealed record EditCommentRequest(string? BodyMd);
