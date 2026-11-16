using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Domain;

namespace Trekky.Modules.Identity.Application.Accounts;

/// <summary>A sign-up request that passed <see cref="SignUpRequestValidator"/>, trimmed and with defaults applied.</summary>
internal sealed record SignUpInput(string Email, string Password, string OrganizationName, string DisplayName)
{
    /// <summary>Call only after the request passed validation.</summary>
    public static SignUpInput From(SignUpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = request.Email!.Trim();
        var displayName = request.DisplayName?.Trim();
        return new SignUpInput(
            email,
            request.Password!,
            request.OrganizationName!.Trim(),
            string.IsNullOrEmpty(displayName) ? DefaultDisplayName(email) : displayName);
    }

    /// <summary>The part of the email before <c>@</c>, cut to the display-name limit.</summary>
    public static string DefaultDisplayName(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var at = email.IndexOf('@', StringComparison.Ordinal);
        var local = at > 0 ? email[..at] : email;
        return local.Length > User.DisplayNameMaxLength ? local[..User.DisplayNameMaxLength] : local;
    }
}
