namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>
/// Finds users of the current organization for other modules (ADR-0009: they use only these public contracts), for
/// example to show who made a change. A user who is not a member of the request's organization is not found.
/// </summary>
public interface IUserDirectory
{
    /// <summary>The members among <paramref name="userIds"/>, by id; anyone else is missing from the result.</summary>
    Task<IReadOnlyDictionary<Guid, UserRef>> FindMembersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
