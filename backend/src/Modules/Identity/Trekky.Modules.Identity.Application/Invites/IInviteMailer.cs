namespace Trekky.Modules.Identity.Application.Invites;

/// <summary>Sends invite emails over SMTP (SMTP2Go in production, Mailpit locally; ADR-0027).</summary>
internal interface IInviteMailer
{
    /// <summary>
    /// <c>true</c> if the mail server accepted the message; <c>false</c> when no server is configured or sending failed
    /// (logged). Never throws for mail problems: the invite stands, and its link is returned to the inviter.
    /// </summary>
    Task<bool> SendAsync(InviteEmail email, CancellationToken cancellationToken);
}
