namespace Trekky.Modules.Identity.Application.Accounts;

internal enum LoginOutcome
{
    Succeeded,
    InvalidCredentials,
    LockedOut,

    /// <summary>The password is right, but the user belongs to no organization (removed, TREK-48); no session is set.</summary>
    NoOrganization,
}
