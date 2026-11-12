namespace Trekky.Modules.WorkItems.Application.Comments;

internal enum CommentOutcome
{
    Succeeded,

    /// <summary>The comment does not exist, was deleted, or belongs to another organization.</summary>
    NotFound,

    /// <summary>Only the author may edit or delete a comment (US-12 AC2).</summary>
    NotAuthor,

    /// <summary><c>If-Match</c> does not match the comment's current version.</summary>
    Stale,
}
