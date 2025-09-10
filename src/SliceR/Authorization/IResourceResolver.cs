namespace SliceR.Authorization;

public interface IResourceResolver<in TRequest, TResource>
{
    Task<TResource?> ResolveAsync(TRequest request, CancellationToken cancellationToken);
}