namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>
/// A created or resent invite with its link (TREK-47, ADR-0027). The link is only ever returned here, once: the API keeps
/// just its hash. <c>EmailSent</c> is <c>false</c> when no mail server is configured or sending failed; the inviter
/// can then pass the link on themselves.
/// </summary>
public sealed record SentInvite(Invite Invite, string AcceptUrl, bool EmailSent);
