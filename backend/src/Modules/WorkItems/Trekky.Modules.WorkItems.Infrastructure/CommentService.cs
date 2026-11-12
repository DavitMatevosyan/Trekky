using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.WorkItems.Application.Comments;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.Modules.WorkItems.Application.WorkItems;
using Trekky.Modules.WorkItems.Domain;
using Trekky.Modules.WorkItems.Persistence;
using Trekky.Persistence.Outbox;
using Trekky.SharedKernel;
using Trekky.SharedKernel.Events;

namespace Trekky.Modules.WorkItems.Infrastructure;

internal sealed class CommentService(WorkItemsDbContext db, ITenantContext tenant, TimeProvider time, IUserDirectory users)
    : ICommentService
{
    public async Task<Comment?> AddAsync(Guid workItemId, AddCommentRequest request, Guid authorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var orgId = tenant.OrgId ?? throw new InvalidOperationException("Adding a comment needs a tenant.");

        // Another organization's item is invisible under RLS, so it is "not found" too.
        var item = await db.WorkItems.AsNoTracking()
            .Where(w => w.Id == workItemId)
            .Select(w => new ItemRef(w.Id, w.ProjectId, w.ProjectKey, w.Number))
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            return null;
        }

        var now = time.GetUtcNow();
        var comment = new WorkItemComment
        {
            Id = Guid.CreateVersion7(now),
            OrgId = orgId,
            WorkItemId = item.Id,
            AuthorId = authorId,
            BodyMd = request.BodyMd!,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Comments.Add(comment);

        db.AddActivityEvent(Event(
            WorkItemEventTypes.CommentAdded,
            now,
            orgId,
            authorId,
            new ItemRef(item.Id, item.ProjectId, item.ProjectKey, item.Number),
            new JsonObject { ["commentId"] = comment.Id, ["excerpt"] = WorkItemComment.Excerpt(comment.BodyMd) }));

        // One SaveChanges is one transaction: the comment and its event commit together, or neither does.
        await db.SaveChangesAsync(cancellationToken);

        // Read back, so the answer has what was stored (Postgres keeps microseconds, not .NET's ticks).
        return await GetAsync(comment.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Comment {comment.Id} was saved but cannot be read back.");
    }

    public async Task<CommentPage?> ListAsync(Guid workItemId, int limit, CommentCursor? after, CancellationToken cancellationToken)
    {
        if (!await db.WorkItems.AnyAsync(w => w.Id == workItemId, cancellationToken))
        {
            return null;
        }

        var query = db.Comments.AsNoTracking().Where(c => c.WorkItemId == workItemId);
        if (after is not null)
        {
            // Keyset: rows after the previous page's last (created_at, id), oldest first, as a row-value comparison.
            query = query.Where(c => EF.Functions.GreaterThan(
                ValueTuple.Create(c.CreatedAt, c.Id), ValueTuple.Create(after.CreatedAt, after.Id)));
        }

        var rows = await query.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var page = rows.Take(limit).ToList();
        var next = rows.Count > limit ? new CommentCursor(page[^1].CreatedAt, page[^1].Id).Encode() : null;
        return new CommentPage(await WithAuthorsAsync(page, cancellationToken), next);
    }

    public async Task<Comment?> GetAsync(Guid commentId, CancellationToken cancellationToken)
        => await db.Comments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken) is { } comment
            ? (await WithAuthorsAsync([comment], cancellationToken))[0]
            : null;

    public async Task<CommentResult> EditAsync(
        Guid commentId, string bodyMd, string ifMatch, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bodyMd);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Locked until commit, so two edits of one comment cannot both pass the version check.
        var comment = await db.FindCommentForUpdateAsync(commentId, cancellationToken);
        if (comment is null)
        {
            return new CommentResult(CommentOutcome.NotFound);
        }

        if (comment.AuthorId != actorId)
        {
            return new CommentResult(CommentOutcome.NotAuthor);
        }

        if (!VersionedWorkItem.IfMatchAccepts(ifMatch, comment.Version))
        {
            return new CommentResult(CommentOutcome.Stale, await GetAsync(commentId, cancellationToken));
        }

        if (bodyMd == comment.BodyMd)
        {
            // Nothing changed: no write, no event, and the ETag stays valid.
            return new CommentResult(CommentOutcome.Succeeded, await GetAsync(commentId, cancellationToken));
        }

        var now = time.GetUtcNow();
        comment.BodyMd = bodyMd;
        comment.UpdatedAt = now;
        db.AddActivityEvent(Event(
            WorkItemEventTypes.CommentEdited,
            now,
            comment.OrgId,
            actorId,
            await ItemOfAsync(comment, cancellationToken),
            new JsonObject { ["commentId"] = comment.Id, ["excerpt"] = WorkItemComment.Excerpt(bodyMd) }));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CommentResult(CommentOutcome.Succeeded, await GetAsync(commentId, cancellationToken));
    }

    public async Task<CommentResult> DeleteAsync(Guid commentId, Guid actorId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var comment = await db.FindCommentForUpdateAsync(commentId, cancellationToken);
        if (comment is null)
        {
            return new CommentResult(CommentOutcome.NotFound);
        }

        if (comment.AuthorId != actorId)
        {
            return new CommentResult(CommentOutcome.NotAuthor);
        }

        var now = time.GetUtcNow();
        comment.DeletedAt = now;
        db.AddActivityEvent(Event(
            WorkItemEventTypes.CommentDeleted,
            now,
            comment.OrgId,
            actorId,
            await ItemOfAsync(comment, cancellationToken),
            new JsonObject { ["commentId"] = comment.Id }));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Read back: Postgres keeps microseconds, so this is exactly the time the event carries.
        var deletedAt = await db.Comments.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == commentId)
            .Select(c => c.DeletedAt)
            .SingleAsync(cancellationToken);
        return new CommentResult(CommentOutcome.Succeeded, Deleted: new DeletedComment(commentId, deletedAt!.Value));
    }

    private async Task<ItemRef> ItemOfAsync(WorkItemComment comment, CancellationToken cancellationToken)
        => await db.WorkItems.AsNoTracking()
               .Where(w => w.Id == comment.WorkItemId)
               .Select(w => new ItemRef(w.Id, w.ProjectId, w.ProjectKey, w.Number))
               .FirstOrDefaultAsync(cancellationToken)
           ?? throw new InvalidOperationException($"Comment {comment.Id} has no visible work item.");

    /// <summary>Comment events use the item as their entity, so they are part of its history (docs/tech/activity-events.md).</summary>
    private static ActivityEnvelope Event(
        string eventType, DateTimeOffset now, Guid orgId, Guid actorId, ItemRef item, JsonObject payload)
        => ActivityEnvelope.Create(
            eventType,
            now,
            orgId,
            EventActor.User(actorId),
            new EventEntity(WorkItem.EntityType, item.Id, WorkItemKey.Format(item.ProjectKey, item.Number)),
            item.ProjectId,
            payload);

    private async Task<List<Comment>> WithAuthorsAsync(IReadOnlyList<WorkItemComment> comments, CancellationToken cancellationToken)
    {
        var members = await users.FindMembersAsync(comments.Select(c => c.AuthorId).Distinct().ToList(), cancellationToken);
        return comments
            .Select(c => new Comment(
                c.Id,
                c.WorkItemId,
                c.AuthorId,
                members.GetValueOrDefault(c.AuthorId) is { } author ? new WorkItemUser(author.Id, author.DisplayName, author.Email) : null,
                c.BodyMd,
                c.CreatedAt,
                c.UpdatedAt,
                c.UpdatedAt > c.CreatedAt,
                VersionedWorkItem.ETagFor(c.Version)))
            .ToList();
    }

    private sealed record ItemRef(Guid Id, Guid ProjectId, string ProjectKey, int Number);
}
