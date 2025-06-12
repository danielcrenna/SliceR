namespace SliceR.Authorization;

internal sealed class AuthorizationFailedException(string failedRequirement, string[] errors)
    : Exception($"Authorization failed for requirement {failedRequirement}. Errors: {string.Join("; ", errors ?? Array.Empty<string>())}")
{
    public string FailedRequirement { get; } = failedRequirement;
    public string[] Errors { get; } = errors ?? [];
}