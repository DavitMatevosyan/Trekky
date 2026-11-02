namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>A merge-patch field: <see cref="IsSet"/> is false when the field was not sent, so it stays as it is.</summary>
internal readonly record struct PatchValue<T>(bool IsSet, T Value)
{
    public static PatchValue<T> Unset => default;

    public static PatchValue<T> Set(T value) => new(true, value);
}
