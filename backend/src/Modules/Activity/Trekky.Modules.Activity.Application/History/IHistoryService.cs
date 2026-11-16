using Trekky.Modules.Activity.Application.Contracts;

namespace Trekky.Modules.Activity.Application.History;

/// <summary>Reads the activity log of the current organization (the request's tenant).</summary>
internal interface IHistoryService
{
    /// <summary>
    /// The item's events, newest first, with actor names (US-09); <c>null</c> when the item does not exist or belongs
    /// to another organization.
    /// </summary>
    Task<HistoryPage?> GetWorkItemHistoryAsync(Guid workItemId, int limit, HistoryCursor? after, CancellationToken cancellationToken);
}
