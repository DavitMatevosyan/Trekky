namespace Trekky.Modules.GitHub.Application.Docs;

/// <summary>A docs request's outcome and, when <see cref="DocsOutcome.Ok"/>, its value.</summary>
internal sealed record DocsResult<T>(DocsOutcome Outcome, T? Value = default)
    where T : class
{
    public static DocsResult<T> Ok(T value) => new(DocsOutcome.Ok, value);
}
