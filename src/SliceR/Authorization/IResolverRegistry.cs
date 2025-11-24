namespace SliceR.Authorization;

internal interface IResolverRegistry
{
    IReadOnlyDictionary<Type, Type> GetMappings();
}