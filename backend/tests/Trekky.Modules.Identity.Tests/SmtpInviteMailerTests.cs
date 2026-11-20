using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Trekky.Modules.Identity.Application.Invites;
using Trekky.Modules.Identity.Infrastructure;

namespace Trekky.Modules.Identity.Tests;

public sealed class SmtpInviteMailerTests
{
    private static readonly InviteEmail Email = new(
        "ada@example.com",
        "Engines <& Co>",
        "Charles",
        "viewer",
        "https://trekky.example/invite#abc",
        new DateTimeOffset(2026, 11, 27, 9, 30, 0, TimeSpan.Zero));

    [Fact]
    public void The_email_names_the_inviter_organization_role_link_and_expiry()
    {
        var message = SmtpInviteMailer.Compose(Email, "Trekky <noreply@example.com>");

        Assert.Equal("Charles invited you to Engines <& Co> on Trekky", message.Subject);
        Assert.Equal("ada@example.com", Assert.Single(message.To.Mailboxes).Address);
        Assert.Equal("noreply@example.com", Assert.Single(message.From.Mailboxes).Address);
        Assert.Contains("as a Viewer", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("https://trekky.example/invite#abc", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("27 November 2026, 09:30 UTC", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void The_html_body_escapes_names()
    {
        var message = SmtpInviteMailer.Compose(Email, "Trekky <noreply@example.com>");

        Assert.Contains("Engines &lt;&amp; Co&gt;", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<& Co>", message.HtmlBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, 1025, SecureSocketOptions.Auto)]
    [InlineData("user", 587, SecureSocketOptions.StartTls)]
    [InlineData("user", 2525, SecureSocketOptions.StartTls)]
    [InlineData("user", 465, SecureSocketOptions.SslOnConnect)]
    public void A_login_is_only_ever_sent_over_tls(string? username, int port, SecureSocketOptions expected)
        => Assert.Equal(expected, SmtpInviteMailer.Security(new SmtpOptions { Host = "smtp.example", Port = port, Username = username }));

    [Fact]
    public async Task Without_a_mail_server_nothing_is_sent_and_the_caller_is_told()
    {
        var mailer = new SmtpInviteMailer(Options.Create(new SmtpOptions { Host = " " }), NullLogger<SmtpInviteMailer>.Instance);

        Assert.False(await mailer.SendAsync(Email, CancellationToken.None));
    }

    [Fact]
    public async Task An_unreachable_mail_server_is_reported_not_thrown()
    {
        // Port 9 on loopback: nothing listens, so the connection is refused at once.
        var mailer = new SmtpInviteMailer(
            Options.Create(new SmtpOptions { Host = "127.0.0.1", Port = 9 }), NullLogger<SmtpInviteMailer>.Instance);

        Assert.False(await mailer.SendAsync(Email, CancellationToken.None));
    }
}
