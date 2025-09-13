namespace SliceR.Authorization;

public sealed class AuthorizationFailedException : Exception
{
    public AuthorizationFailedException()
        : base("Authorization failed.")
    {
        FailedRequirement = string.Empty;
        Errors = [];
    }

    public AuthorizationFailedException(string message)
        : base(message)
    {
        FailedRequirement = string.Empty;
        Errors = [];
    }

    public AuthorizationFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
        FailedRequirement = string.Empty;
        Errors = [];
    }

    public AuthorizationFailedException(string failedRequirement, string[]? errors)
        : base($"Authorization failed for requirement {failedRequirement}. Errors: {string.Join("; ", errors ?? [])}")
    {
        FailedRequirement = failedRequirement;
        Errors = errors ?? [];
    }

    public string FailedRequirement { get; }
    public IReadOnlyList<string> Errors { get; }
}
