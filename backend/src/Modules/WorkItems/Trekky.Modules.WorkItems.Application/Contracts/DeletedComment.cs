namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>Answer to <c>DELETE /api/v1/comments/{commentId}</c>: when it was deleted (server time, the time of its <c>CommentDeleted</c> event).</summary>
public sealed record DeletedComment(Guid Id, DateTimeOffset DeletedAt);
