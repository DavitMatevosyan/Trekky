using Trekky.Modules.WorkItems.Application.Contracts;

namespace Trekky.Modules.WorkItems.Application.Comments;

/// <summary>Comments on work items of the current organization (US-12).</summary>
internal interface ICommentService
{
    /// <summary>
    /// Adds the comment and its <c>CommentAdded</c> event in one transaction (US-12 AC3); <c>null</c> when the item does not
    /// exist or belongs to another organization. Input must be validated.
    /// </summary>
    Task<Comment?> AddAsync(Guid workItemId, AddCommentRequest request, Guid authorId, CancellationToken cancellationToken);

    /// <summary>The item's comments, oldest first (US-12 AC1); <c>null</c> when the item does not exist.</summary>
    Task<CommentPage?> ListAsync(Guid workItemId, int limit, CommentCursor? after, CancellationToken cancellationToken);

    /// <summary>The comment, or <c>null</c> when it does not exist, was deleted or belongs to another organization.</summary>
    Task<Comment?> GetAsync(Guid commentId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the body if <paramref name="actorId"/> wrote the comment and <paramref name="ifMatch"/> is its current ETag,
    /// and records <c>CommentEdited</c> on the item in the same transaction (US-12 AC2). An unchanged body changes nothing.
    /// Input must be validated.
    /// </summary>
    Task<CommentResult> EditAsync(Guid commentId, string bodyMd, string ifMatch, Guid actorId, CancellationToken cancellationToken);

    /// <summary>Soft-deletes the comment if <paramref name="actorId"/> wrote it, and records <c>CommentDeleted</c> on the item (US-12 AC2).</summary>
    Task<CommentResult> DeleteAsync(Guid commentId, Guid actorId, CancellationToken cancellationToken);
}
