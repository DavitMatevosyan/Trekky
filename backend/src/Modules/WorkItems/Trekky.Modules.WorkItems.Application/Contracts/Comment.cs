namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>A comment on a work item (US-12 AC1).</summary>
/// <param name="Author">The author; <c>null</c> when they are no longer a member of the organization.</param>
/// <param name="Edited">Whether the comment was changed after it was posted (US-12 AC2).</param>
public sealed record Comment(
    Guid Id, Guid WorkItemId, Guid AuthorId, WorkItemUser? Author, string BodyMd, DateTimeOffset CreatedAt, bool Edited);
