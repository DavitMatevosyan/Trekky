using FluentValidation;
using Trekky.Modules.GitHub.Application.Contracts;

namespace Trekky.Modules.GitHub.Application.Connections;

/// <summary>The code and installation id from GitHub's redirect must be present; GitHub checks the rest.</summary>
internal sealed class ConnectGitHubRequestValidator : AbstractValidator<ConnectGitHubRequest>
{
    /// <summary>GitHub's codes are 20 characters; anything far longer is not one.</summary>
    public const int CodeMaxLength = 256;

    public ConnectGitHubRequestValidator()
    {
        RuleFor(r => r.Code)
            .NotEmpty().WithMessage("The code from GitHub is missing. Start the connection again.")
            .MaximumLength(CodeMaxLength).WithMessage("The code from GitHub is not valid. Start the connection again.")
            .OverridePropertyName("code");

        RuleFor(r => r.InstallationId)
            .NotNull().WithMessage("The installation id from GitHub is missing. Start the connection again.")
            .GreaterThan(0).WithMessage("The installation id from GitHub is not valid. Start the connection again.")
            .OverridePropertyName("installationId");
    }
}
