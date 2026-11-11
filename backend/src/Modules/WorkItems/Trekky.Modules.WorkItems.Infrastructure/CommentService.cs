using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.WorkItems.Application.Comments;
using Trekky.Modules.WorkItems.Application.Contracts;
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
            .Select(w => new { w.Id, w.ProjectId, w.ProjectKey, w.Number })
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

        // Comment events use the item as their entity, so they are part of its history (docs/tech/activity-events.md).
        db.AddActivityEvent(ActivityEnvelope.Create(
            WorkItemEventTypes.CommentAdded,
            now,
            orgId,
            EventActor.User(authorId),
            new EventEntity(WorkItem.EntityType, item.Id, WorkItemKey.Format(item.ProjectKey, item.Number)),
            item.ProjectId,
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
                c.UpdatedAt > c.CreatedAt))
            .ToList();
    }
}
