namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>
/// The <c>Smtp</c> configuration section (ADR-0027): Mailpit locally (<c>localhost:1025</c>, no login), SMTP2Go in
/// production. Without a <see cref="Host"/>, no mail is sent and invite links are only shown to the inviter.
/// </summary>
internal sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }

    /// <summary>587 (STARTTLS) by default; 465 means TLS from the start. TLS is used whenever the server offers it.</summary>
    public int Port { get; set; } = 587;

    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>The sender, e.g. <c>Trekky &lt;noreply@example.com&gt;</c>; SMTP2Go accepts only verified sender domains.</summary>
    public string From { get; set; } = "Trekky <noreply@trekky.local>";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
