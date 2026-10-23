using Microsoft.AspNetCore.Identity;

namespace Trekky.Modules.Identity.Domain;

/// <summary>An ASP.NET Core Identity user. Not tenant-scoped: a user can join several organizations later (v1.1).</summary>
internal sealed class User : IdentityUser<Guid>
{
    public User()
    {
        Id = Guid.CreateVersion7();
    }

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}
