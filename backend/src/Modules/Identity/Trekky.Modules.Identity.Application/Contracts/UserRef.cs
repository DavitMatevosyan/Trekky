namespace Trekky.Modules.Identity.Application.Contracts;

public sealed record UserRef(Guid Id, string DisplayName, string? Email);
