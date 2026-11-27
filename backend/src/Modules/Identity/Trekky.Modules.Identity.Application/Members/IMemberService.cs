using Trekky.Modules.Identity.Application.Contracts;

namespace Trekky.Modules.Identity.Application.Members;

/// <summary>Changing members' roles and removing members of the caller's organization (TREK-48). Implemented in Infrastructure.</summary>
internal interface IMemberService
{
    /// <summary>
    /// Gives the member <paramref name="role"/> if the caller may and the organization keeps an Owner, and revokes the
    /// member's tokens so their next request carries the new role. Returns the member as they are now, also when stale.
    /// </summary>
    Task<(MemberChangeOutcome Outcome, Member? Member)> ChangeRoleAsync(
        Guid userId, string role, string ifMatch, MemberActor actor, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the member if the caller may and the organization keeps an Owner: their tokens and sessions end at once, and
    /// their name stays on their past work.
    /// </summary>
    Task<MemberChangeOutcome> RemoveAsync(Guid userId, MemberActor actor, CancellationToken cancellationToken);
}
