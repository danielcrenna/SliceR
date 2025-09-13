namespace SliceR.Authorization;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AuthorizedAttribute : Attribute
{
    public string? PolicyName { get; }

    public AuthorizedAttribute(string policyName)
    {
        if (string.IsNullOrEmpty(policyName))
            throw new ArgumentException("Policy name cannot be an empty string. Use null or the parameterless constructor for authentication-only scenarios.", nameof(policyName));

        PolicyName = policyName;
    }

    public AuthorizedAttribute()
    {
        PolicyName = null;
    }
}
