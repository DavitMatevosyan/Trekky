using FluentValidation;
using Trekky.Modules.Projects.Application.Contracts;
using ProjectEntity = Trekky.Modules.Projects.Domain.Project;

namespace Trekky.Modules.Projects.Application.Projects;

/// <summary>US-05 AC1: name 1–80 characters (after trimming), key matching <see cref="ProjectEntity.KeyRegex"/>.</summary>
internal sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(r => r.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Name is required.")
            .Must(name => name is null || name.Trim().Length <= ProjectEntity.NameMaxLength)
            .WithMessage($"Name must be at most {ProjectEntity.NameMaxLength} characters.")
            .Must(name => name is null || !name.Contains('\0', StringComparison.Ordinal))
            .WithMessage("Name contains a character that is not allowed.")
            .OverridePropertyName("name");

        RuleFor(r => r.Key)
            .NotEmpty().WithMessage("Key is required.")
            .Matches(ProjectEntity.KeyRegex)
            .WithMessage("Key must be 2–10 characters: an uppercase letter, then uppercase letters or digits.")
            .OverridePropertyName("key");
    }
}
