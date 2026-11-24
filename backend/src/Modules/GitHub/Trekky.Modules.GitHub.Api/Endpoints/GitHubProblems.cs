using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Trekky.Modules.GitHub.Api.Endpoints;

/// <summary>Problem details the GitHub endpoints share.</summary>
internal static class GitHubProblems
{
    public static ProblemHttpResult NotConfigured()
        => TypedResults.Problem(
            title: "GitHub is not set up",
            detail: "This Trekky server has no GitHub App configured, so GitHub cannot be connected yet.",
            statusCode: StatusCodes.Status503ServiceUnavailable);

    public static ProblemHttpResult Unavailable()
        => TypedResults.Problem(
            title: "GitHub did not answer",
            detail: "GitHub could not be reached or returned an error. Try again in a moment.",
            statusCode: StatusCodes.Status502BadGateway);

    public static ProblemHttpResult ConnectionNotFound()
        => TypedResults.Problem(title: "GitHub connection not found", statusCode: StatusCodes.Status404NotFound);

    public static ProblemHttpResult InstallationGone()
        => TypedResults.Problem(
            title: "GitHub installation removed",
            detail: "The Trekky app was uninstalled or suspended on GitHub. Disconnect it here and install it again.",
            statusCode: StatusCodes.Status409Conflict);

    /// <summary>A setting GitHub refused, shown on its field (422 with <c>errors</c>, like a validation problem).</summary>
    public static ProblemHttpResult FieldRejected(string field, string message)
        => TypedResults.Problem(
            title: "GitHub refused the settings",
            detail: message,
            statusCode: StatusCodes.Status422UnprocessableEntity,
            extensions: new Dictionary<string, object?> { ["errors"] = new Dictionary<string, string[]> { [field] = [message] } });
}
