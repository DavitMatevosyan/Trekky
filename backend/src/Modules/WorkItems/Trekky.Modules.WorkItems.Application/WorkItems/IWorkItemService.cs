using Trekky.Modules.WorkItems.Application.Contracts;

namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>Work items of the current organization (the request's tenant).</summary>
internal interface IWorkItemService
{
    /// <summary>
    /// Creates the item in To Do with the project's next key, and its <c>WorkItemCreated</c> event, in one transaction
    /// (US-06). Input must be validated.
    /// </summary>
    Task<WorkItemResult> CreateAsync(Guid projectId, CreateWorkItemRequest request, Guid actorId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the edit if <paramref name="ifMatch"/> is the current ETag, re-checking the parent rules, and records a
    /// <c>WorkItemUpdated</c> event when something changed (US-07). Input must be validated.
    /// </summary>
    Task<WorkItemResult> UpdateAsync(
        Guid workItemId, WorkItemPatch patch, string ifMatch, Guid actorId, CancellationToken cancellationToken);

    /// <summary>The project's items, newest update first; <c>null</c> when the project does not exist.</summary>
    Task<WorkItemPage?> ListAsync(Guid projectId, int limit, WorkItemCursor? after, CancellationToken cancellationToken);

    /// <summary>The item, or <c>null</c> when it does not exist or belongs to another organization.</summary>
    Task<VersionedWorkItem?> GetAsync(Guid workItemId, CancellationToken cancellationToken);

    /// <summary>The item with this key (<c>TRK-42</c>), or <c>null</c>.</summary>
    Task<VersionedWorkItem?> GetByKeyAsync(string key, CancellationToken cancellationToken);
}
