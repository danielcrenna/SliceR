namespace SliceR.Authorization;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ResolveResourceAttribute : Attribute
{
    public Type ResolverType { get; }

    public ResolveResourceAttribute(Type resolverType)
    {
        ArgumentNullException.ThrowIfNull(resolverType);

        var isValid = resolverType.GetInterfaces().Any(i =>
            i.IsGenericType &&
            i.GetGenericTypeDefinition() == typeof(IResourceResolver<,>));

        if (!isValid)
            throw new ArgumentException($"Type {resolverType.Name} must implement IResourceResolver<,>", nameof(resolverType));

        ResolverType = resolverType;
    }
}
