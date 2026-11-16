namespace Trekky.Modules.Identity.Application.Accounts;

internal enum LoginOutcome
{
    Succeeded,
    InvalidCredentials,
    LockedOut,
}
