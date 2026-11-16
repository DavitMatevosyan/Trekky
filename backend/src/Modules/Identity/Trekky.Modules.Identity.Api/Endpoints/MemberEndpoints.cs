using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Trekky.Modules.Identity.Application.Contracts;

namespace Trekky.Modules.Identity.Api.Endpoints;

/// <summary>The caller's organization's members under /api/v1 (OpenAPI draft, TRK-5; US-13).</summary>
internal static class MemberEndpoints
{
    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/members", ListAsync)
            .RequireAuthorization()
            .WithTags("Members")
            .WithName("ListMembers")
            .WithSummary("Members of the caller's organization, by display name (assignee picker, US-13)")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<Ok<MemberList>> ListAsync(IUserDirectory users, CancellationToken cancellationToken)
        => TypedResults.Ok(new MemberList(await users.ListMembersAsync(cancellationToken)));
}
