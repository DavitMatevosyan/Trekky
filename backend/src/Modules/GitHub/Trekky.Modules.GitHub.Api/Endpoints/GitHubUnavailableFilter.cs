using Microsoft.AspNetCore.Http;
using Trekky.Modules.GitHub.Application.GitHub;

namespace Trekky.Modules.GitHub.Api.Endpoints;

/// <summary>Answers 502 problem details when GitHub fails mid-request, instead of a 500.</summary>
internal static class GitHubUnavailableFilter
{
    public static async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        try
        {
            return await next(context);
        }
        catch (GitHubUnavailableException)
        {
            return GitHubProblems.Unavailable();
        }
    }
}
