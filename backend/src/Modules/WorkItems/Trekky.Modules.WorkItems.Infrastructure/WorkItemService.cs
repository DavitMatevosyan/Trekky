using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Projects.Application.Contracts;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.Modules.WorkItems.Application.WorkItems;
using Trekky.Modules.WorkItems.Domain;
using Trekky.Modules.WorkItems.Persistence;
using Trekky.Persistence.Outbox;
using Trekky.SharedKernel;
using Trekky.SharedKernel.Events;

namespace Trekky.Modules.WorkItems.Infrastructure;

internal sealed class WorkItemService(
    WorkItemsDbContext db,
    ITenantContext tenant,
    TimeProvider time,
    IProjectDirectory projects,
    IUserDirectory users)
    : IWorkItemService
{
    public async Task<WorkItemResult> CreateAsync(
        Guid projectId, CreateWorkItemRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var orgId = tenant.OrgId ?? throw new InvalidOperationException("Creating a work item needs a tenant.");

        // Another organization's project is invisible under RLS, so it is "not found" too.
        var project = await projects.FindAsync(projectId, cancellationToken);
        if (project is null)
        {
            return WorkItemResult.NotFound;
        }

        var type = request.Type!;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Locked, so the parent's type cannot change before this item commits (see ItemLocks).
        var parent = request.ParentId is { } parentId ? await db.FindParentForShareAsync(parentId, projectId, cancellationToken) : null;
        if (CheckParent(type, request.ParentId, parent, self: null) is { } violation)
        {
            return new WorkItemResult(WorkItemOutcome.ParentNotAllowed, Violation: violation);
        }

        var now = time.GetUtcNow();
        var item = new WorkItem
        {
            Id = Guid.CreateVersion7(now),
            OrgId = orgId,
            ProjectId = project.Id,
            ProjectKey = project.Key,
            Number = await db.TakeNextAsync(project.Id, orgId, cancellationToken),
            Type = type,
            Title = WorkItemFieldRules.NormalizeTitle(request.Title!),
            DescriptionMd = WorkItemFieldRules.NormalizeDescription(request.DescriptionMd),
            Status = WorkItemStatuses.ToDo, // US-06 AC3
            ParentId = parent?.Id,
            CreatedBy = actorId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.WorkItems.Add(item);

        // The event commits with the item, or neither does.
        db.AddActivityEvent(Event(WorkItemEventTypes.WorkItemCreated, item, actorId, now, new JsonObject
        {
            ["type"] = item.Type,
            ["title"] = item.Title,
            ["status"] = item.Status,
            ["parentId"] = parent?.Id,
            ["parentKey"] = parent?.Key,
        }));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await SucceededAsync(item.Id, cancellationToken);
    }

    public async Task<WorkItemResult> UpdateAsync(
        Guid workItemId, WorkItemPatch patch, string ifMatch, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(patch);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Locked until commit: concurrent edits queue here, and no child can be added while the type is re-checked.
        var item = await db.FindForUpdateAsync(workItemId, cancellationToken);
        if (item is null)
        {
            return WorkItemResult.NotFound;
        }

        if (!VersionedWorkItem.IfMatchAccepts(ifMatch, item.Version))
        {
            return new WorkItemResult(WorkItemOutcome.Stale, await GetAsync(workItemId, cancellationToken));
        }

        var type = patch.Type.IsSet ? patch.Type.Value! : item.Type;
        var title = patch.Title.IsSet ? WorkItemFieldRules.NormalizeTitle(patch.Title.Value!) : item.Title;
        var description = patch.DescriptionMd.IsSet ? WorkItemFieldRules.NormalizeDescription(patch.DescriptionMd.Value) : item.DescriptionMd;
        var parentId = patch.ParentId.IsSet ? patch.ParentId.Value : item.ParentId;

        var changes = new JsonObject();
        if (type != item.Type)
        {
            changes["type"] = Change(item.Type, type);
        }

        if (title != item.Title)
        {
            changes["title"] = Change(item.Title, title);
        }

        if (description != item.DescriptionMd)
        {
            // The full text would bloat history; the item itself has it (docs/tech/activity-events.md).
            changes["descriptionMd"] = new JsonObject { ["changed"] = true };
        }

        // US-07 AC2: a new type or parent re-checks the rules for the item and its children.
        if (type != item.Type || parentId != item.ParentId)
        {
            var parent = parentId is { } id ? await db.FindParentForShareAsync(id, item.ProjectId, cancellationToken) : null;
            if (CheckParent(type, parentId, parent, self: item.Id) is { } violation)
            {
                return new WorkItemResult(WorkItemOutcome.ParentNotAllowed, Violation: violation);
            }

            if (type != item.Type && await CheckChildrenAsync(item, type, cancellationToken) is { } childViolation)
            {
                return new WorkItemResult(WorkItemOutcome.ChildrenNotAllowed, Violation: childViolation);
            }

            if (parentId != item.ParentId)
            {
                var previous = item.ParentId is { } previousId ? await FindInProjectAsync(previousId, item.ProjectId, cancellationToken) : null;
                changes["parent"] = new JsonObject { ["from"] = ParentRef(previous), ["to"] = ParentRef(parent) };
            }
        }

        if (changes.Count == 0)
        {
            // Nothing changed: no write and no event, and the ETag stays valid.
            return await SucceededAsync(workItemId, cancellationToken);
        }

        var now = time.GetUtcNow();
        item.Type = type;
        item.Title = title;
        item.DescriptionMd = description;
        item.ParentId = parentId;
        item.UpdatedAt = now;
        db.AddActivityEvent(Event(WorkItemEventTypes.WorkItemUpdated, item, actorId, now, new JsonObject { ["changes"] = changes }));

        // xmin is also the EF concurrency token, a second guard behind the row lock.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await SucceededAsync(workItemId, cancellationToken);
    }

    public async Task<WorkItemResult> ChangeStatusAsync(
        Guid workItemId, string to, string ifMatch, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(to);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Locked until commit, so two moves of the same item cannot both pass the transition check.
        var item = await db.FindForUpdateAsync(workItemId, cancellationToken);
        if (item is null)
        {
            return WorkItemResult.NotFound;
        }

        if (!VersionedWorkItem.IfMatchAccepts(ifMatch, item.Version))
        {
            return new WorkItemResult(WorkItemOutcome.Stale, await GetAsync(workItemId, cancellationToken));
        }

        // US-10 AC3: a disallowed move, including to the status it already has, lists where it can go instead.
        if (!StatusWorkflow.Allows(item.Status, to))
        {
            var allowed = StatusWorkflow.AllowedTransitions(item.Status);
            return new WorkItemResult(
                WorkItemOutcome.TransitionNotAllowed,
                Transition: new TransitionViolation(
                    $"{item.Key} cannot move from {StatusLabel(item.Status)} to {StatusLabel(to)}. Allowed: {string.Join(", ", allowed.Select(StatusLabel))}.",
                    allowed));
        }

        var now = time.GetUtcNow();
        var from = item.Status;
        item.Status = to;
        item.UpdatedAt = now;
        db.AddActivityEvent(Event(WorkItemEventTypes.WorkItemStatusChanged, item, actorId, now, Change(from, to)));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await SucceededAsync(workItemId, cancellationToken);
    }

    public async Task<WorkItemPage?> ListAsync(
        Guid projectId, WorkItemFilter filter, int limit, WorkItemCursor? after, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (await projects.FindAsync(projectId, cancellationToken) is null)
        {
            return null;
        }

        IQueryable<WorkItem> query = db.WorkItems.AsNoTracking().Where(w => w.ProjectId == projectId);
        if (filter.Types.Count > 0)
        {
            var types = filter.Types;
            query = query.Where(w => types.Contains(w.Type));
        }

        // A search that is a key of this project (TRK-1): that item comes first on the first page, so it can always be
        // found by its key however many other keys start the same way (TRK-10, TRK-100, ...). Later pages leave it out.
        SummaryRow? exact = null;
        if (filter.Query is { } key && WorkItemKey.TryParse(key.ToUpperInvariant(), out var keyProject, out var keyNumber))
        {
            if (after is null)
            {
                exact = await Summaries(query.Where(w => w.ProjectKey == keyProject && w.Number == keyNumber))
                    .FirstOrDefaultAsync(cancellationToken);
            }

            query = query.Where(w => !(w.ProjectKey == keyProject && w.Number == keyNumber));
        }

        if (filter.Query is { } search)
        {
            // LIKE wildcards in the search are literal characters, not patterns.
            var escaped = search.Replace(@"\", @"\\", StringComparison.Ordinal)
                .Replace("%", @"\%", StringComparison.Ordinal)
                .Replace("_", @"\_", StringComparison.Ordinal);
            query = query.Where(w =>
                EF.Functions.ILike(w.Title, "%" + escaped + "%", @"\")
                || EF.Functions.ILike(w.ProjectKey + "-" + w.Number.ToString(), escaped + "%", @"\"));
        }

        if (after is not null)
        {
            // Keyset: rows after the previous page's last (updated_at, id), newest first, as a row-value comparison.
            query = query.Where(w => EF.Functions.LessThan(
                ValueTuple.Create(w.UpdatedAt, w.Id), ValueTuple.Create(after.UpdatedAt, after.Id)));
        }

        var size = exact is null ? limit : limit - 1;
        var rows = await Summaries(query.OrderByDescending(w => w.UpdatedAt).ThenByDescending(w => w.Id).Take(size + 1))
            .ToListAsync(cancellationToken);

        var page = rows.Take(size).ToList();
        // With limit 1 the exact item fills the first page alone; the next page then starts before every row.
        var next = rows.Count <= size ? null
            : page.Count == 0 ? new WorkItemCursor(DateTimeOffset.MaxValue, Guid.AllBitsSet).Encode()
            : new WorkItemCursor(page[^1].Item.UpdatedAt, page[^1].Item.Id).Encode();
        return new WorkItemPage((exact is null ? page : page.Prepend(exact)).Select(ToSummary).ToList(), next);
    }

    public Task<VersionedWorkItem?> GetAsync(Guid workItemId, CancellationToken cancellationToken)
        => GetDetailAsync(db.WorkItems.Where(w => w.Id == workItemId), cancellationToken);

    public Task<VersionedWorkItem?> GetByKeyAsync(string key, CancellationToken cancellationToken)
        => WorkItemKey.TryParse(key, out var projectKey, out var number)
            ? GetDetailAsync(db.WorkItems.Where(w => w.ProjectKey == projectKey && w.Number == number), cancellationToken)
            : Task.FromResult<VersionedWorkItem?>(null);

    private async Task<VersionedWorkItem?> GetDetailAsync(IQueryable<WorkItem> match, CancellationToken cancellationToken)
    {
        var row = await Summaries(match.AsNoTracking()).FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var item = row.Item;
        var parent = item.ParentId is { } parentId
            ? await Summaries(db.WorkItems.AsNoTracking().Where(w => w.Id == parentId)).FirstOrDefaultAsync(cancellationToken)
            : null;
        var children = await Summaries(db.WorkItems.AsNoTracking().Where(w => w.ParentId == item.Id).OrderBy(w => w.Number))
            .ToListAsync(cancellationToken);
        var creator = (await users.FindMembersAsync([item.CreatedBy], cancellationToken)).GetValueOrDefault(item.CreatedBy) is { } user
            ? new WorkItemUser(user.Id, user.DisplayName, user.Email)
            : null;

        var detail = new WorkItemDetail(
            item.Id,
            item.Key,
            item.Type,
            item.Title,
            item.Status,
            row.ParentKey,
            item.UpdatedAt,
            item.ProjectId,
            item.DescriptionMd,
            parent is null ? null : ToSummary(parent),
            children.Select(ToSummary).ToList(),
            StatusWorkflow.AllowedTransitions(item.Status),
            item.CreatedAt,
            creator);
        return new VersionedWorkItem(detail, item.Version);
    }

    /// <summary>Items with their parent's key. Parents are always in the same project, so the parent's number is enough.</summary>
    private IQueryable<SummaryRow> Summaries(IQueryable<WorkItem> items)
        => items.Select(w => new SummaryRow(
            w,
            db.WorkItems.Where(p => p.Id == w.ParentId).Select(p => (int?)p.Number).FirstOrDefault()));

    private async Task<WorkItemResult> SucceededAsync(Guid workItemId, CancellationToken cancellationToken)
        => await GetAsync(workItemId, cancellationToken) is { } item
            ? new WorkItemResult(WorkItemOutcome.Succeeded, item)
            : throw new InvalidOperationException($"Work item {workItemId} was saved but cannot be read back.");

    private Task<WorkItem?> FindInProjectAsync(Guid id, Guid projectId, CancellationToken cancellationToken)
        => db.WorkItems.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id && w.ProjectId == projectId, cancellationToken);

    /// <summary>US-06 AC4, AC5: the parent exists in the item's project and its type is allowed for the item's type.</summary>
    private static RuleViolation? CheckParent(string type, Guid? parentId, WorkItem? parent, Guid? self)
    {
        var allowed = ParentRules.AllowedParentTypes(type);
        string? detail = null;
        if (parentId is null)
        {
            detail = ParentRules.RequiresParent(type) ? $"{WithArticle(type)} needs a parent: {Labels(allowed)}." : null;
        }
        else if (parentId == self)
        {
            detail = "An item cannot be its own parent.";
        }
        else if (parent is null)
        {
            detail = "The parent must be a work item in the same project.";
        }
        else if (!ParentRules.Allows(type, parent.Type))
        {
            detail = allowed.Count == 0
                ? $"{WithArticle(type)} cannot have a parent."
                : $"{WithArticle(type)} can only have a parent of type {Labels(allowed)}, not {Label(parent.Type)}.";
        }

        return detail is null ? null : new RuleViolation(detail, allowed, []);
    }

    /// <summary>US-07 AC2: every child must still accept the item as its parent once it has <paramref name="newType"/>.</summary>
    private async Task<RuleViolation?> CheckChildrenAsync(WorkItem item, string newType, CancellationToken cancellationToken)
    {
        var children = await db.WorkItems.AsNoTracking()
            .Where(w => w.ParentId == item.Id)
            .OrderBy(w => w.Number)
            .Select(w => new { w.Number, w.Type })
            .ToListAsync(cancellationToken);

        var conflicting = children
            .Where(child => !ParentRules.Allows(child.Type, newType))
            .Select(child => WorkItemKey.Format(item.ProjectKey, child.Number))
            .ToList();

        return conflicting.Count == 0
            ? null
            : new RuleViolation(
                $"{WithArticle(newType)} cannot be the parent of {string.Join(", ", conflicting)}. Move them first.",
                // The item's own parent is fine; what conflicts are the children, so no parent types to offer.
                [],
                conflicting);
    }

    private static ActivityEnvelope Event(string eventType, WorkItem item, Guid actorId, DateTimeOffset now, JsonObject payload)
        => ActivityEnvelope.Create(
            eventType, now, item.OrgId, EventActor.User(actorId), new EventEntity(WorkItem.EntityType, item.Id, item.Key), item.ProjectId, payload);

    private static JsonObject Change(string from, string to) => new() { ["from"] = from, ["to"] = to };

    private static JsonObject? ParentRef(WorkItem? parent)
        => parent is null ? null : new JsonObject { ["id"] = parent.Id, ["key"] = parent.Key };

    private static string Label(string type) => type switch
    {
        WorkItemTypes.Epic => "Epic",
        WorkItemTypes.Story => "Story",
        WorkItemTypes.Task => "Task",
        WorkItemTypes.SubTask => "SubTask",
        WorkItemTypes.Bug => "Bug",
        WorkItemTypes.TechDebt => "TechDebt",
        _ => type,
    };

    private static string StatusLabel(string status) => status switch
    {
        WorkItemStatuses.ToDo => "To Do",
        WorkItemStatuses.InProgress => "In Progress",
        WorkItemStatuses.InReview => "In Review",
        WorkItemStatuses.Done => "Done",
        _ => status,
    };

    private static string WithArticle(string type) => type == WorkItemTypes.Epic ? "An Epic" : $"A {Label(type)}";

    private static string Labels(IEnumerable<string> types) => string.Join(" or ", types.Select(Label));

    private static WorkItemSummary ToSummary(SummaryRow row)
        => new(
            row.Item.Id,
            row.Item.Key,
            row.Item.Type,
            row.Item.Title,
            row.Item.Status,
            row.ParentKey,
            row.Item.UpdatedAt);

    private sealed record SummaryRow(WorkItem Item, int? ParentNumber)
    {
        public string? ParentKey => ParentNumber is { } number ? WorkItemKey.Format(Item.ProjectKey, number) : null;
    }
}
