using FluentValidation;
using Trekky.Modules.WorkItems.Application.Contracts;

namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>US-06 AC1: a known type, a title of 1–200 characters (after trimming), an optional Markdown description.</summary>
internal sealed class CreateWorkItemRequestValidator : AbstractValidator<CreateWorkItemRequest>
{
    public CreateWorkItemRequestValidator()
    {
        RuleFor(r => r.Type).ValidType().OverridePropertyName("type");
        RuleFor(r => r.Title).ValidTitle().OverridePropertyName("title");
        RuleFor(r => r.DescriptionMd).ValidDescription().OverridePropertyName("descriptionMd");
    }
}
