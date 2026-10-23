using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Trekky.Modules.Identity.Auth;
using Trekky.Modules.Identity.Contracts;
using Trekky.Modules.Identity.Domain;
using Trekky.SharedKernel;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Trekky.Modules.Identity.Endpoints;

/// <summary>Sign-up, login, logout and the current user, under /api/v1 (OpenAPI draft, TRK-5).</summary>
internal static class AuthEndpoints
{
    internal const string EmailTakenMessage = "An account with this email already exists.";
    internal const string InvalidCredentialsMessage = "Email or password is incorrect.";
    internal static readonly string LockedOutMessage =
        $"Too many failed attempts. Try again in {IdentityServiceCollectionExtensions.LockoutDuration.TotalMinutes:0} minutes.";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/sign-up", SignUpAsync)
            .AllowAnonymous()
            .WithName("SignUp")
            .WithSummary("Create a user, an organization and an Owner membership (US-01)");

        auth.MapPost("/login", LogInAsync)
            .AllowAnonymous()
            .WithName("LogIn")
            .WithSummary("Check credentials and set the session cookie (US-02)");

        auth.MapPost("/logout", LogOutAsync)
            .RequireAuthorization()
            .WithName("LogOut")
            .WithSummary("End the session and revoke the caller's refresh tokens");

        endpoints.MapGet("/me", GetMeAsync)
            .RequireAuthorization()
            .WithTags("Auth")
            .WithName("GetMe")
            .WithSummary("The current user, organization and role");

        return endpoints;
    }

    private static async Task<Results<Created<Me>, ValidationProblem, ProblemHttpResult>> SignUpAsync(
        SignUpRequest request,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        if (!SignUpValidation.TryValidate(request, out var input, out var errors))
        {
            return TypedResults.ValidationProblem(errors);
        }

        var (outcome, me) = await accounts.SignUpAsync(input, cancellationToken);
        return outcome == SignUpOutcome.Created && me is not null
            ? TypedResults.Created("/api/v1/me", me)
            : TypedResults.Problem(title: "Email already registered", detail: EmailTakenMessage, statusCode: StatusCodes.Status409Conflict);
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> LogInAsync(
        LoginRequest request,
        AccountService accounts)
    {
        if (!SignUpValidation.TryValidate(request, out var errors))
        {
            return TypedResults.ValidationProblem(errors);
        }

        return await accounts.LogInAsync(request.Email!, request.Password!) switch
        {
            LoginOutcome.Succeeded => TypedResults.NoContent(),
            LoginOutcome.LockedOut => TypedResults.Problem(
                title: "Account locked", detail: LockedOutMessage, statusCode: StatusCodes.Status423Locked),
            _ => TypedResults.Problem(
                title: "Login failed", detail: InvalidCredentialsMessage, statusCode: StatusCodes.Status401Unauthorized),
        };
    }

    /// <summary>Logs out everywhere: revokes all of the user's tokens and invalidates their session cookies.</summary>
    private static async Task<NoContent> LogOutAsync(
        ClaimsPrincipal principal,
        UserManager<User> users,
        SessionRevoker sessions,
        SignInManager<User> signIn,
        CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue(Claims.Subject);
        var user = string.IsNullOrEmpty(subject) ? null : await users.FindByIdAsync(subject);
        if (user is not null)
        {
            await sessions.RevokeAllAsync(user, cancellationToken);
        }

        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<Me>, UnauthorizedHttpResult>> GetMeAsync(
        ClaimsPrincipal principal,
        ITenantContext tenant,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(Claims.Subject), out var userId) || tenant.OrgId is not { } orgId)
        {
            return TypedResults.Unauthorized();
        }

        var me = await accounts.GetMeAsync(userId, orgId, cancellationToken);
        return me is null ? TypedResults.Unauthorized() : TypedResults.Ok(me);
    }
}
