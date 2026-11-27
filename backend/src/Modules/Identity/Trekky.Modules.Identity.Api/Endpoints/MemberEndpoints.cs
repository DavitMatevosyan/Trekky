using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Application.Members;
using Trekky.SharedKernel;

namespace Trekky.Modules.Identity.Api.Endpoints;

/// <summary>
/// The caller's organization's members under /api/v1 (OpenAPI draft, TRK-5; US-13): everyone reads the list; Admins and
/// Owners change roles and remove members (TREK-48, ManageMembers), within the rules of <c>MemberRules</c>.
/// </summary>
internal static class MemberEndpoints
{
    internal const string RoleNotAllowedMessage = "Only an Owner can make or remove Admins and Owners.";
    internal const string LastOwnerMessage =
        "Your organization needs at least one Owner. Make someone else an Owner first.";
    internal const string NotFoundMessage = "There is no such member in your organization.";
    internal const string StaleMessage = "This member was changed since you loaded the page. Their latest role is shown.";

    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var members = endpoints.MapGroup("/members")
            .WithTags("Members")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        members.MapGet("/", ListAsync)
            .RequireAuthorization(TrekkyPolicies.ReadWork)
            .WithName("ListMembers")
            .WithSummary("Members of the caller's organization, by display name, with role, kind and etag (US-13, TREK-48)");

        members.MapPatch("/{userId:guid}", ChangeRoleAsync)
            .RequireAuthorization(TrekkyPolicies.ManageMembers)
            .WithName("ChangeMemberRole")
            .WithSummary("Change a member's role (TREK-48)")
            .WithDescription(
                "Admins change Members and Viewers; only an Owner makes or removes Admins and Owners (403). The last Owner " +
                "keeps the role (409). Needs If-Match with the member's etag (428, 412). The member's tokens are revoked, so " +
                "their next request signs in again with the new role. Records MemberRoleChanged.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        members.MapDelete("/{userId:guid}", RemoveAsync)
            .RequireAuthorization(TrekkyPolicies.ManageMembers)
            .WithName("RemoveMember")
            .WithSummary("Remove a member from the organization (TREK-48)")
            .WithDescription(
                "Admins remove Members and Viewers; only an Owner removes Admins and Owners (403). The last Owner stays " +
                "(409). The member's tokens and sessions end at once; their name stays on their past work. Records MemberRemoved.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<Ok<MemberList>> ListAsync(IUserDirectory users, CancellationToken cancellationToken)
        => TypedResults.Ok(new MemberList(await users.ListMembersAsync(cancellationToken)));

    private static async Task<Results<Ok<Member>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> ChangeRoleAsync(
        Guid userId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ChangeMemberRoleRequest request,
        IValidator<ChangeMemberRoleRequest> validator,
        IMemberService members,
        ClaimsPrincipal principal,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (Actor(principal) is not { } actor)
        {
            return TypedResults.Unauthorized();
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        if (string.IsNullOrEmpty(ifMatch))
        {
            return Problem(
                "If-Match required", "Send the member's etag from the members list in If-Match.", StatusCodes.Status428PreconditionRequired);
        }

        var (outcome, member) = await members.ChangeRoleAsync(userId, request.Role!, ifMatch, actor, cancellationToken);
        if (member is not null)
        {
            // On success the new version; when stale the latest one, to retry with.
            response.Headers.ETag = member.Etag;
        }

        return outcome == MemberChangeOutcome.Done && member is not null ? TypedResults.Ok(member) : ChangeProblem(outcome);
    }

    private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult>> RemoveAsync(
        Guid userId, IMemberService members, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (Actor(principal) is not { } actor)
        {
            return TypedResults.Unauthorized();
        }

        var outcome = await members.RemoveAsync(userId, actor, cancellationToken);
        return outcome == MemberChangeOutcome.Done ? TypedResults.NoContent() : ChangeProblem(outcome);
    }

    private static MemberActor? Actor(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(TrekkyClaimTypes.Subject), out var userId)
            ? new MemberActor(userId)
            : null;

    private static ProblemHttpResult ChangeProblem(MemberChangeOutcome outcome) => outcome switch
    {
        MemberChangeOutcome.NotFound => Problem("Member not found", NotFoundMessage, StatusCodes.Status404NotFound),
        MemberChangeOutcome.RoleNotAllowed => Problem("Role not allowed", RoleNotAllowedMessage, StatusCodes.Status403Forbidden),
        MemberChangeOutcome.LastOwner => Problem("Last Owner", LastOwnerMessage, StatusCodes.Status409Conflict),
        MemberChangeOutcome.Stale => Problem("Changed elsewhere", StaleMessage, StatusCodes.Status412PreconditionFailed),
        _ => throw new InvalidOperationException($"No problem response for {outcome}."),
    };

    private static ProblemHttpResult Problem(string title, string detail, int status)
        => TypedResults.Problem(title: title, detail: detail, statusCode: status);
}
