namespace Trekky.SharedKernel.Events;

/// <summary>Who caused an event: <c>user</c>, <c>agent</c> or <c>system</c> (no id).</summary>
public sealed record EventActor(string Type, Guid? Id)
{
    public const string UserType = "user";

    public static EventActor User(Guid userId) => new(UserType, userId);
}
