namespace Trekky.Modules.GitHub.Application.GitHub;

/// <summary>GitHub could not be reached or answered with an error Trekky cannot act on; the API answers 502.</summary>
internal sealed class GitHubUnavailableException : Exception
{
    public GitHubUnavailableException()
    {
    }

    public GitHubUnavailableException(string message)
        : base(message)
    {
    }

    public GitHubUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
