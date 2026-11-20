namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>The <c>Invites</c> configuration section (TREK-47).</summary>
internal sealed class InviteOptions
{
    public const string SectionName = "Invites";

    /// <summary>The web app's public origin for invite links; defaults to the web client's first redirect URI's origin.</summary>
    public string? LinkBaseUrl { get; set; }
}
