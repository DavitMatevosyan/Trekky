using Trekky.Modules.WorkItems.Application.Contracts;

namespace Trekky.Modules.WorkItems.Application.Comments;

/// <summary>
/// The result of editing or deleting a comment: the comment when edited (its latest version when stale), or when it
/// was deleted.
/// </summary>
internal sealed record CommentResult(CommentOutcome Outcome, Comment? Comment = null, DeletedComment? Deleted = null);
