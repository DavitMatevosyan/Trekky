using Trekky.Modules.Activity.Application.ActivityEvents;
using Trekky.Modules.Activity.Application.Contracts;
using Trekky.Modules.Activity.Application.History;
using Trekky.Modules.Activity.Persistence;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.SharedKernel.Events;

namespace Trekky.Modules.Activity.Infrastructure;

internal sealed class HistoryService(ActivityHistoryReader reader, IWorkItemDirectory workItems, IUserDirectory users) : IHistoryService
{
    public async Task<HistoryPage?> GetWorkItemHistoryAsync(
        Guid workItemId, int limit, HistoryCursor? after, CancellationToken cancellationToken)
    {
        // Another organization's item is invisible under RLS, so it is "not found" too.
        if (!await workItems.ExistsAsync(workItemId, cancellationToken))
        {
            return null;
        }

        // Comment events use the item as their entity (docs/tech/activity-events.md), so they are part of this history.
        var rows = await reader.ReadAsync(workItemId, after?.EventId, limit + 1, cancellationToken);
        var page = rows.Take(limit).ToList();

        var actorIds = page
            .Where(e => e is { ActorType: EventActor.UserType, ActorId: not null })
            .Select(e => e.ActorId!.Value)
            .Distinct()
            .ToList();
        var members = await users.FindMembersAsync(actorIds, cancellationToken);

        var items = page
            .Select(e => ActivityEventMapper.ToHistoryEvent(e, ActorName(e.ActorType, e.ActorId, members)))
            .ToList();
        var next = rows.Count > limit ? new HistoryCursor(page[^1].EventId).Encode() : null;
        return new HistoryPage(items, next);
    }

    private static string? ActorName(string actorType, Guid? actorId, IReadOnlyDictionary<Guid, UserRef> members)
        => actorType == EventActor.UserType && actorId is { } id ? members.GetValueOrDefault(id)?.DisplayName : null;
}
