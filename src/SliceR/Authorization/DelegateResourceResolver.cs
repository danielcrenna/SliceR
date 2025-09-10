namespace SliceR.Authorization;

internal sealed class DelegateResourceResolver<TRequest, TResource>(
    Func<TRequest, IServiceProvider, CancellationToken, Task<TResource?>> resolver,
    IServiceProvider serviceProvider) : IResourceResolver<TRequest, TResource>
{
    public Task<TResource?> ResolveAsync(TRequest request, CancellationToken cancellationToken)
    {
        return resolver(request, serviceProvider, cancellationToken);
    }
}