namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>A page of an item's comments, oldest first; <see cref="NextCursor"/> is <c>null</c> on the last page.</summary>
public sealed record CommentPage(IReadOnlyList<Comment> Items, string? NextCursor);
