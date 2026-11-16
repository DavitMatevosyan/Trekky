namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>
/// Body of <c>POST /api/v1/work-items/{workItemId}/move</c>: a card dropped on the board (US-11 AC3, AC4). The neighbours are
/// the cards the client saw directly around the drop; the move is refused with 409 when they are no longer neighbours.
/// </summary>
/// <param name="Status">The column: the current status to reorder, or another one the workflow allows.</param>
/// <param name="AfterId">The card directly above in that column; <c>null</c> for the top. Always <c>null</c> for Done.</param>
/// <param name="BeforeId">The card directly below in that column; <c>null</c> for the bottom. Always <c>null</c> for Done.</param>
public sealed record MoveWorkItemRequest(string? Status, Guid? AfterId, Guid? BeforeId);
