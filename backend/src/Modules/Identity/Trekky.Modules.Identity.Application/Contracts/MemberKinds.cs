namespace Trekky.Modules.Identity.Application.Contracts;

/// <summary>Whether a member is a person or a bot, as <see cref="Member.Kind"/> says. Bots come with TREK-49; until then every member is a person.</summary>
public static class MemberKinds
{
    public const string Person = "person";

    public const string Bot = "bot";
}
