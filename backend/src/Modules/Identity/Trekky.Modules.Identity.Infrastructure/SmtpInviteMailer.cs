using System.Globalization;
using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Trekky.Modules.Identity.Application.Invites;
using Trekky.Modules.Identity.Domain;

namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>Sends invite emails with MailKit over SMTP (ADR-0027). Mail problems are logged, never thrown.</summary>
internal sealed partial class SmtpInviteMailer(IOptions<SmtpOptions> options, ILogger<SmtpInviteMailer> logger) : IInviteMailer
{
    /// <summary>For the whole exchange (connect, log in, send), so a stuck server cannot hold the inviter's request long.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task<bool> SendAsync(InviteEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        var smtp = options.Value;
        if (!smtp.IsConfigured || smtp.Host is null)
        {
            LogNotConfigured(logger);
            return false;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Timeout);
        try
        {
            using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
            await client.ConnectAsync(smtp.Host, smtp.Port, Security(smtp), deadline.Token);
            if (!string.IsNullOrEmpty(smtp.Username))
            {
                await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, deadline.Token);
            }

            await client.SendAsync(Compose(email, smtp.From), deadline.Token);
            await client.DisconnectAsync(quit: true, deadline.Token);
            return true;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogSendFailed(logger, smtp.Host, smtp.Port, exception);
            return false;
        }
    }

    /// <summary>
    /// A password is never sent in the clear: with a login, TLS is required (from the start on port 465, STARTTLS
    /// otherwise), so a stripped STARTTLS fails instead of falling back. Without a login (Mailpit), TLS only if offered.
    /// </summary>
    internal static SecureSocketOptions Security(SmtpOptions smtp)
        => string.IsNullOrEmpty(smtp.Username) ? SecureSocketOptions.Auto
            : smtp.Port == 465 ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

    internal static MimeMessage Compose(InviteEmail email, string from)
    {
        var role = RoleName(email.Role);
        var expires = email.ExpiresAt.UtcDateTime.ToString("d MMMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var message = new MimeMessage
        {
            Subject = $"{email.InviterName} invited you to {email.OrganizationName} on Trekky",
        };
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(email.To));

        var builder = new BodyBuilder
        {
            TextBody =
                $"{email.InviterName} invited you to join {email.OrganizationName} on Trekky as {role}.\n\n" +
                $"Accept the invite and create your account:\n{email.AcceptUrl}\n\n" +
                $"The link works once and expires on {expires}. If you did not expect this email, you can ignore it.\n",
            HtmlBody =
                $"<p>{Html(email.InviterName)} invited you to join <strong>{Html(email.OrganizationName)}</strong> on Trekky as {Html(role)}.</p>" +
                $"<p><a href=\"{Html(email.AcceptUrl)}\">Accept the invite and create your account</a></p>" +
                $"<p>The link works once and expires on {Html(expires)}. If you did not expect this email, you can ignore it.</p>",
        };
        message.Body = builder.ToMessageBody();
        return message;
    }

    private static string RoleName(string role) => role switch
    {
        Roles.Admin => "an Admin",
        Roles.Viewer => "a Viewer",
        _ => "a Member",
    };

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    [LoggerMessage(Level = LogLevel.Information, Message = "No SMTP server is configured (Smtp:Host); the invite link is only shown to the inviter")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sending an invite email through {Host}:{Port} failed; the invite stays and its link is shown to the inviter")]
    private static partial void LogSendFailed(ILogger logger, string host, int port, Exception exception);
}
