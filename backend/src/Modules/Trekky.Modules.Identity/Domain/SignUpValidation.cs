using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using Trekky.Modules.Identity.Contracts;

namespace Trekky.Modules.Identity.Domain;

/// <summary>A sign-up request that passed validation, trimmed and with defaults applied.</summary>
internal sealed record SignUpInput(string Email, string Password, string OrganizationName, string DisplayName);

/// <summary>Input rules for sign-up and login (US-01, US-02, OpenAPI draft).</summary>
internal static class SignUpValidation
{
    public const int EmailMaxLength = 254;
    public const int PasswordMinLength = 12;
    public const int PasswordMaxLength = 128;
    public const int DisplayNameMaxLength = 80;

    /// <summary>Validates a sign-up request. Errors are keyed by the JSON field name.</summary>
    public static bool TryValidate(
        SignUpRequest request,
        [NotNullWhen(true)] out SignUpInput? input,
        out Dictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(request);
        errors = [];

        var email = request.Email?.Trim() ?? string.Empty;
        if (email.Length == 0)
        {
            errors["email"] = ["Email is required."];
        }
        else if (!IsValidEmail(email))
        {
            errors["email"] = ["Enter a valid email address."];
        }

        var password = request.Password ?? string.Empty;
        if (password.Length < PasswordMinLength)
        {
            errors["password"] = [$"Password must be at least {PasswordMinLength} characters."];
        }
        else if (password.Length > PasswordMaxLength)
        {
            errors["password"] = [$"Password must be at most {PasswordMaxLength} characters."];
        }

        var organizationName = request.OrganizationName?.Trim() ?? string.Empty;
        if (organizationName.Length == 0)
        {
            errors["organizationName"] = ["Organization name is required."];
        }
        else if (organizationName.Length > Organization.NameMaxLength)
        {
            errors["organizationName"] = [$"Organization name must be at most {Organization.NameMaxLength} characters."];
        }

        var displayName = request.DisplayName?.Trim();
        if (displayName?.Length > DisplayNameMaxLength)
        {
            errors["displayName"] = [$"Display name must be at most {DisplayNameMaxLength} characters."];
        }

        if (errors.Count > 0)
        {
            input = null;
            return false;
        }

        input = new SignUpInput(
            email,
            password,
            organizationName,
            string.IsNullOrEmpty(displayName) ? DefaultDisplayName(email) : displayName);
        return true;
    }

    /// <summary>Validates a login request: both fields present. Credentials themselves are checked by Identity.</summary>
    public static bool TryValidate(LoginRequest request, out Dictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(request);
        errors = [];

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = ["Email is required."];
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            errors["password"] = ["Password is required."];
        }

        return errors.Count == 0;
    }

    /// <summary>A single plain address (<c>name@domain</c>), at most 254 characters, no display-name form.</summary>
    public static bool IsValidEmail(string email)
        => email.Length <= EmailMaxLength
           && email.IndexOf('@', StringComparison.Ordinal) > 0
           && MailAddress.TryCreate(email, out var address)
           && string.Equals(address.Address, email, StringComparison.Ordinal);

    /// <summary>The part of the email before <c>@</c>, cut to the display-name limit.</summary>
    public static string DefaultDisplayName(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        var local = at > 0 ? email[..at] : email;
        return local.Length > DisplayNameMaxLength ? local[..DisplayNameMaxLength] : local;
    }
}
