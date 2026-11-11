using FluentValidation;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>
/// US-11: a known status, no position in Done (always last finished first), and neighbours that are two other cards: not
/// the same one twice, and not the moved card (its id comes from the route, through <see cref="ContextFor"/>). Whether
/// the move is allowed and the neighbours are still next to each other is the service's check (422, 409).
/// </summary>
internal sealed class MoveWorkItemRequestValidator : AbstractValidator<MoveWorkItemRequest>
{
    public const string DonePositionMessage = "Done is ordered by when items finished; leave afterId and beforeId empty.";

    public const string SameNeighbourMessage = "afterId and beforeId must be different cards.";

    public const string SelfNeighbourMessage = "A card cannot be its own neighbour.";

    private const string WorkItemIdKey = "workItemId";

    /// <summary>The context to validate <paramref name="request"/> for moving <paramref name="workItemId"/>.</summary>
    public static ValidationContext<MoveWorkItemRequest> ContextFor(MoveWorkItemRequest request, Guid workItemId)
    {
        var context = new ValidationContext<MoveWorkItemRequest>(request);
        context.RootContextData[WorkItemIdKey] = workItemId;
        return context;
    }

    public MoveWorkItemRequestValidator()
    {
        RuleFor(r => r.Status)
            .Must(WorkItemStatuses.IsKnown)
            .WithMessage(ChangeStatusRequestValidator.StatusMessage)
            .OverridePropertyName("status");
        RuleFor(r => r.AfterId)
            .Null()
            .When(r => r.Status == WorkItemStatuses.Done)
            .WithMessage(DonePositionMessage)
            .OverridePropertyName("afterId");
        RuleFor(r => r.BeforeId)
            .Null()
            .When(r => r.Status == WorkItemStatuses.Done)
            .WithMessage(DonePositionMessage)
            .OverridePropertyName("beforeId");
        RuleFor(r => r.BeforeId)
            .NotEqual(r => r.AfterId)
            .When(r => r.AfterId is not null)
            .WithMessage(SameNeighbourMessage)
            .OverridePropertyName("beforeId");
        RuleFor(r => r.AfterId)
            .Must((_, id, context) => !IsMovedCard(id, context))
            .WithMessage(SelfNeighbourMessage)
            .OverridePropertyName("afterId");
        RuleFor(r => r.BeforeId)
            .Must((_, id, context) => !IsMovedCard(id, context))
            .WithMessage(SelfNeighbourMessage)
            .OverridePropertyName("beforeId");
    }

    private static bool IsMovedCard(Guid? id, ValidationContext<MoveWorkItemRequest> context)
        => id is not null && context.RootContextData.TryGetValue(WorkItemIdKey, out var moved) && moved is Guid movedId && movedId == id;
}
