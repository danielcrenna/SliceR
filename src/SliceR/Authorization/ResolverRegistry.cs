namespace SliceR.Authorization;

internal sealed class ResolverRegistry : IResolverRegistry
{
    private readonly IReadOnlyDictionary<Type, Type> _mappings;

    public ResolverRegistry(IReadOnlyDictionary<Type, Type> mappings)
    {
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    public IReadOnlyDictionary<Type, Type> GetMappings() => _mappings;
}