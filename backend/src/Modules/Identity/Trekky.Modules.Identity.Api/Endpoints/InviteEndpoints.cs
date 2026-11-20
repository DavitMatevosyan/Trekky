using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Application.Invites;
using Trekky.SharedKernel;

namespace Trekky.Modules.Identity.Api.Endpoints;

/// <summary>
/// Invites (TREK-47, ADR-0027): Admins and Owners manage them under <c>/invites</c> (ManageMembers); the invite page
/// reads and accepts one anonymously under <c>/auth/invites</c>, with the token from the link in the request body.
/// </summary>
internal static class InviteEndpoints
{
    internal const string RoleNotAllowedMessage = "Only an Owner can invite Admins or change their invites.";
    internal const string AlreadyMemberMessage = "This person is already a member of your organization.";
    internal const string EmailHasAccountMessage =
        "This email already belongs to another organization. A person can be in only one organization.";
    internal const string AlreadyInvitedMessage = "This email already has an open invite. Resend it instead.";
    internal const string NotOpenMessage = "This invite was already accepted or withdrawn.";
    internal const string ConflictMessage = "This invite changed at the same moment. Reload the page and try again.";
    internal const string LinkNotFoundMessage =
        "This invite link is not valid. If you got a newer invite email, use the link in that one.";
    internal const string LinkExpiredMessage = "This invite has expired. Ask the person who invited you to send a new one.";
    internal const string LinkRevokedMessage = "This invite was withdrawn. Ask the person who invited you if you still need access.";
    internal const string LinkUsedMessage = "This invite has already been used. Log in instead.";
    internal const string AccountExistsMessage = "An account with this email already exists. Log in instead.";

    public static IEndpointRouteBuilder MapInviteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var invites = endpoints.MapGroup("/invites")
            .WithTags("Invites")
            .RequireAuthorization(TrekkyPolicies.ManageMembers)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        invites.MapGet("/", ListAsync)
            .WithName("ListInvites")
            .WithSummary("Open invites of the caller's organization, newest first (TREK-47)");

        invites.MapPost("/", CreateAsync)
            .WithName("CreateInvite")
            .WithSummary("Invite someone by email with a role; emails a single-use link that expires in 7 days (TREK-47)")
            .ProducesProblem(StatusCodes.Status409Conflict);

        invites.MapPost("/{inviteId:guid}/resend", ResendAsync)
            .WithName("ResendInvite")
            .WithSummary("Send an open invite again with a new link; the old link stops working (TREK-47)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        invites.MapDelete("/{inviteId:guid}", RevokeAsync)
            .WithName("RevokeInvite")
            .WithSummary("Withdraw an open invite; its link then says so (TREK-47)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var link = endpoints.MapGroup("/auth/invites").WithTags("Auth");

        link.MapPost("/preview", PreviewAsync)
            .AllowAnonymous()
            .WithName("PreviewInvite")
            .WithSummary("What an invite link is for: email, role, organization and inviter (TREK-47)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone);

        link.MapPost("/accept", AcceptAsync)
            .AllowAnonymous()
            .WithName("AcceptInvite")
            .WithSummary("Create the account and join the inviter's organization with the invited role; sets the session cookie (TREK-47)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);

        return endpoints;
    }

    private static async Task<Ok<InviteList>> ListAsync(IInviteService invites, CancellationToken cancellationToken)
        => TypedResults.Ok(await invites.ListAsync(cancellationToken));

    private static async Task<Results<Created<SentInvite>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> CreateAsync(
        CreateInviteRequest request,
        IValidator<CreateInviteRequest> validator,
        IInviteService invites,
        ClaimsPrincipal principal,
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

        var (outcome, sent) = await invites.CreateAsync(request.Email!, request.Role!, actor, cancellationToken);
        return outcome switch
        {
            CreateInviteOutcome.Created when sent is not null => TypedResults.Created((string?)null, sent),
            CreateInviteOutcome.RoleNotAllowed => Problem("Role not allowed", RoleNotAllowedMessage, StatusCodes.Status403Forbidden),
            CreateInviteOutcome.AlreadyMember => Problem("Already a member", AlreadyMemberMessage, StatusCodes.Status409Conflict),
            CreateInviteOutcome.EmailHasAccount => Problem("Email in another organization", EmailHasAccountMessage, StatusCodes.Status409Conflict),
            _ => Problem("Already invited", AlreadyInvitedMessage, StatusCodes.Status409Conflict),
        };
    }

    private static async Task<Results<Ok<SentInvite>, ProblemHttpResult, UnauthorizedHttpResult>> ResendAsync(
        Guid inviteId, IInviteService invites, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (Actor(principal) is not { } actor)
        {
            return TypedResults.Unauthorized();
        }

        var (outcome, sent) = await invites.ResendAsync(inviteId, actor, cancellationToken);
        return outcome == InviteChangeOutcome.Done && sent is not null ? TypedResults.Ok(sent) : ChangeProblem(outcome);
    }

    private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult>> RevokeAsync(
        Guid inviteId, IInviteService invites, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (Actor(principal) is not { } actor)
        {
            return TypedResults.Unauthorized();
        }

        var outcome = await invites.RevokeAsync(inviteId, actor, cancellationToken);
        return outcome == InviteChangeOutcome.Done ? TypedResults.NoContent() : ChangeProblem(outcome);
    }

    private static async Task<Results<Ok<InvitePreview>, ProblemHttpResult>> PreviewAsync(
        InviteTokenRequest request, IInviteService invites, CancellationToken cancellationToken)
    {
        var (outcome, preview) = await invites.PreviewAsync(request.Token ?? string.Empty, cancellationToken);
        return outcome == InviteLinkOutcome.Valid && preview is not null ? TypedResults.Ok(preview) : LinkProblem(outcome);
    }

    private static async Task<Results<Created<Me>, ValidationProblem, ProblemHttpResult>> AcceptAsync(
        AcceptInviteRequest request,
        IValidator<AcceptInviteRequest> validator,
        IInviteService invites,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var (outcome, me) = await invites.AcceptAsync(request.Token!, request.Password!, request.DisplayName, cancellationToken);
        return outcome == InviteLinkOutcome.Valid && me is not null ? TypedResults.Created("/api/v1/me", me) : LinkProblem(outcome);
    }

    private static InviteActor? Actor(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(TrekkyClaimTypes.Subject), out var userId)
            ? new InviteActor(userId, principal.FindFirstValue(TrekkyClaimTypes.Role))
            : null;

    private static ProblemHttpResult ChangeProblem(InviteChangeOutcome outcome) => outcome switch
    {
        InviteChangeOutcome.NotFound => Problem("Invite not found", "There is no such invite in your organization.", StatusCodes.Status404NotFound),
        InviteChangeOutcome.RoleNotAllowed => Problem("Role not allowed", RoleNotAllowedMessage, StatusCodes.Status403Forbidden),
        InviteChangeOutcome.Conflict => Problem("Invite changed", ConflictMessage, StatusCodes.Status409Conflict),
        _ => Problem("Invite closed", NotOpenMessage, StatusCodes.Status409Conflict),
    };

    private static ProblemHttpResult LinkProblem(InviteLinkOutcome outcome) => outcome switch
    {
        InviteLinkOutcome.Expired => Problem("Invite expired", LinkExpiredMessage, StatusCodes.Status410Gone),
        InviteLinkOutcome.Revoked => Problem("Invite withdrawn", LinkRevokedMessage, StatusCodes.Status410Gone),
        InviteLinkOutcome.Used => Problem("Invite used", LinkUsedMessage, StatusCodes.Status410Gone),
        InviteLinkOutcome.EmailHasAccount => Problem("Email already registered", AccountExistsMessage, StatusCodes.Status409Conflict),
        _ => Problem("Invite not found", LinkNotFoundMessage, StatusCodes.Status404NotFound),
    };

    private static ProblemHttpResult Problem(string title, string detail, int status)
        => TypedResults.Problem(title: title, detail: detail, statusCode: status);
}
