using FluentValidation;

namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>The same field rules as create, for the fields an edit sends (US-07).</summary>
internal sealed class WorkItemPatchValidator : AbstractValidator<WorkItemPatch>
{
    public WorkItemPatchValidator()
    {
        RuleFor(p => p.Type.Value).ValidType().When(p => p.Type.IsSet).OverridePropertyName("type");
        RuleFor(p => p.Title.Value).ValidTitle().When(p => p.Title.IsSet).OverridePropertyName("title");
        RuleFor(p => p.DescriptionMd.Value).ValidDescription().When(p => p.DescriptionMd.IsSet).OverridePropertyName("descriptionMd");
    }
}
