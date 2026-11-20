using FluentValidation;
using Trekky.Modules.GitHub.Application.Contracts;
using Trekky.Modules.GitHub.Domain;

namespace Trekky.Modules.GitHub.Application.Repositories;

/// <summary>The connection and repository are required; the docs folder and branch, when sent, must be well formed.</summary>
internal sealed class LinkProjectRepositoryRequestValidator : AbstractValidator<LinkProjectRepositoryRequest>
{
    public LinkProjectRepositoryRequestValidator()
    {
        RuleFor(r => r.InstallationId)
            .Must(id => id is { } value && value != Guid.Empty).WithMessage("Choose a GitHub connection.")
            .OverridePropertyName("installationId");

        RuleFor(r => r.Repository)
            .NotEmpty().WithMessage("Repository is required.")
            .MaximumLength(ProjectRepository.RepositoryMaxLength)
            .WithMessage($"Repository must be at most {ProjectRepository.RepositoryMaxLength} characters.")
            .Matches(ProjectRepository.RepositoryRegex)
            .WithMessage("Repository must be owner/name, as on GitHub.")
            .OverridePropertyName("repository");

        RuleFor(r => r.DocsPath)
            .Must(path => path is null || DocsPath.Normalize(path) is not null)
            .WithMessage($"Docs folder must be a folder inside the repository, like docs/ (at most {DocsPath.MaxLength} characters, no ..).")
            .OverridePropertyName("docsPath");

        RuleFor(r => r.Branch)
            .Must(branch => branch is null || BranchName.IsValid(branch.Trim()))
            .WithMessage("Branch is not a valid Git branch name.")
            .OverridePropertyName("branch");
    }
}
