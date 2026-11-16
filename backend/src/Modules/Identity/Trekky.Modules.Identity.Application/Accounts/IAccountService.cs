using Trekky.Modules.Identity.Application.Contracts;

namespace Trekky.Modules.Identity.Application.Accounts;

/// <summary>Sign-up, login, logout and the current user (US-01, US-02). Implemented in Infrastructure with ASP.NET Core Identity.</summary>
internal interface IAccountService
{
    /// <summary>Creates the user, the organization and the Owner membership in one transaction, then signs the user in.</summary>
    Task<(SignUpOutcome Outcome, Me? Me)> SignUpAsync(SignUpInput input, CancellationToken cancellationToken);

    /// <summary>Checks the password with lockout and sets the session cookie.</summary>
    Task<LoginOutcome> LogInAsync(string email, string password);

    /// <summary>Logs the user out everywhere: revokes their tokens and invalidates their session cookies.</summary>
    Task LogOutAsync(Guid? userId, CancellationToken cancellationToken);

    /// <summary>The current user, organization and role, read as the request's tenant; <c>null</c> if either is gone.</summary>
    Task<Me?> GetMeAsync(Guid userId, Guid orgId, CancellationToken cancellationToken);
}
