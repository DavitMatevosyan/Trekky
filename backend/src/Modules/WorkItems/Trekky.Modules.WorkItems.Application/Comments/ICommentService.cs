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

    /// <summary>The comment, or <c>null</c> when it does not exist or belongs to another organization.</summary>
    Task<Comment?> GetAsync(Guid commentId, CancellationToken cancellationToken);
}
