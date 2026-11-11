using Trekky.Modules.WorkItems.Application.Contracts;

namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>Work items of the current organization (the request's tenant).</summary>
internal interface IWorkItemService
{
    /// <summary>
    /// Creates the item at the top of To Do with the project's next key, and its <c>WorkItemCreated</c> event, in one transaction
    /// (US-06). Input must be validated.
    /// </summary>
    Task<WorkItemResult> CreateAsync(Guid projectId, CreateWorkItemRequest request, Guid actorId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the edit if <paramref name="ifMatch"/> is the current ETag, re-checking the parent rules, and records a
    /// <c>WorkItemUpdated</c> event when something changed (US-07). Input must be validated.
    /// </summary>
    Task<WorkItemResult> UpdateAsync(
        Guid workItemId, WorkItemPatch patch, string ifMatch, Guid actorId, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the item to the top of <paramref name="to"/> if <paramref name="ifMatch"/> is the current ETag and the workflow allows
    /// it, and records a <c>WorkItemStatusChanged</c> event in the same transaction (US-10). Input must be validated.
    /// </summary>
    Task<WorkItemResult> ChangeStatusAsync(
        Guid workItemId, string to, string ifMatch, Guid actorId, CancellationToken cancellationToken);

    /// <summary>
    /// Puts the card in column <paramref name="status"/> between <paramref name="afterId"/> and <paramref name="beforeId"/>
    /// if <paramref name="ifMatch"/> is the current ETag (US-11 AC3, AC4). A new status follows the workflow and records
    /// <c>WorkItemStatusChanged</c>; a reorder alone records no event and leaves <c>updatedAt</c> as it is. Done ignores
    /// position. <see cref="WorkItemOutcome.BoardChanged"/> when the neighbours are no longer next to each other there.
    /// Input must be validated.
    /// </summary>
    Task<WorkItemResult> MoveAsync(
        Guid workItemId, string status, Guid? afterId, Guid? beforeId, string ifMatch, Guid actorId, CancellationToken cancellationToken);

    /// <summary>The project's items that match <paramref name="filter"/>, newest update first; <c>null</c> when the project does not exist.</summary>
    Task<WorkItemPage?> ListAsync(
        Guid projectId, WorkItemFilter filter, int limit, WorkItemCursor? after, CancellationToken cancellationToken);

    /// <summary>
    /// The project's board, each column in rank order (US-11 AC4). Done is last finished
    /// first and holds the latest 20 unless <paramref name="allDone"/> is set; <c>null</c> when the project does not exist.
    /// </summary>
    Task<Board?> GetBoardAsync(Guid projectId, bool allDone, CancellationToken cancellationToken);

    /// <summary>The item, or <c>null</c> when it does not exist or belongs to another organization.</summary>
    Task<VersionedWorkItem?> GetAsync(Guid workItemId, CancellationToken cancellationToken);

    /// <summary>The item with this key (<c>TRK-42</c>), or <c>null</c>.</summary>
    Task<VersionedWorkItem?> GetByKeyAsync(string key, CancellationToken cancellationToken);
}
