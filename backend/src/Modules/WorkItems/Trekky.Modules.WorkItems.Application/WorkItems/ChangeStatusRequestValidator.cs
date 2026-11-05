using FluentValidation;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>US-10 AC1: <c>to</c> is one of the workflow's statuses. Whether the move is allowed is the service's check (422).</summary>
internal sealed class ChangeStatusRequestValidator : AbstractValidator<ChangeStatusRequest>
{
    public static readonly string StatusMessage = $"Status must be one of: {string.Join(", ", WorkItemStatuses.All)}.";

    public ChangeStatusRequestValidator()
        => RuleFor(r => r.To).Must(WorkItemStatuses.IsKnown).WithMessage(StatusMessage).OverridePropertyName("to");
}
