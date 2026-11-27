namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>
/// Finds users of the current organization for other modules (ADR-0009: they use only these public contracts), for
/// example to show who made a change. A user who never belonged to the request's organization is not found.
/// </summary>
public interface IUserDirectory
{
    /// <summary>
    /// The members among <paramref name="userIds"/>, by id, including removed members (TREK-48), so past work keeps
    /// their name; anyone else is missing from the result. To check who may be assigned work, use <see cref="IsMemberAsync"/>.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, UserRef>> FindMembersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>Every current member of the request's organization with their role, ordered by display name (US-13, TREK-48).</summary>
    Task<IReadOnlyList<Member>> ListMembersAsync(CancellationToken cancellationToken);

    /// <summary>Whether <paramref name="userId"/> currently belongs to the request's organization (not removed).</summary>
    Task<bool> IsMemberAsync(Guid userId, CancellationToken cancellationToken);
}
