using System.Reflection;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace SliceR.Authorization;

internal sealed class AuthorizationBehavior<TRequest, TResponse>(
    IAuthorizationProvider provider,
    IHttpContextAccessor accessor,
    IServiceProvider serviceProvider)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var authenticatedAttribute = typeof(TRequest).GetCustomAttribute<AuthenticatedAttribute>();
        if (authenticatedAttribute != null)
        {
            var user = accessor.HttpContext?.User;
            if (user == null || !user.Identity?.IsAuthenticated == true)
            {
                throw new AuthorizationFailedException("Authentication", ["User is not authenticated."]);
            }

            return await next(cancellationToken).ConfigureAwait(false);
        }

        var authorizedAttribute = typeof(TRequest).GetCustomAttribute<AuthorizedAttribute>();
        if (authorizedAttribute != null)
        {
            var user = accessor.HttpContext?.User;
            if (user == null || !user.Identity?.IsAuthenticated == true)
            {
                throw new AuthorizationFailedException("Authentication", ["User is not authenticated."]);
            }

            return await HandleAttributeBasedAuthorization(request, authorizedAttribute, user, next, cancellationToken).ConfigureAwait(false);
        }

        if (request is IAuthorizedRequest<TResponse> authorizedRequest)
        {
            var user = accessor.HttpContext?.User;
            if (user == null || !user.Identity?.IsAuthenticated == true)
            {
                throw new AuthorizationFailedException("Authentication", ["User is not authenticated."]);
            }

            return await HandleInterfaceBasedAuthorization(authorizedRequest, user, next, cancellationToken).ConfigureAwait(false);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }

    private async Task<TResponse> HandleAttributeBasedAuthorization(
        TRequest request,
        AuthorizedAttribute authorizedAttribute,
        ClaimsPrincipal user,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var policyName = authorizedAttribute.PolicyName;

        if (string.IsNullOrWhiteSpace(policyName))
            return await next(cancellationToken).ConfigureAwait(false);

        AuthorizationResult result;

        var resolveResourceAttribute = typeof(TRequest).GetCustomAttribute<ResolveResourceAttribute>();
        if (resolveResourceAttribute != null)
        {
            var resource = await ResolveResourceUsingAttribute(request, resolveResourceAttribute, cancellationToken).ConfigureAwait(false);
            result = await provider.AuthorizeAsync(user, policyName, resource).ConfigureAwait(false);
        }
        else
        {
            result = await provider.AuthorizeAsync(user, policyName).ConfigureAwait(false);
        }

        if (result.Succeeded)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var errors = result.Failure?.FailureReasons
            .Select(reason => reason.Message)
            .ToArray() ?? ["Authorization failed."];

        throw new AuthorizationFailedException(policyName, errors);
    }

    private async Task<TResponse> HandleInterfaceBasedAuthorization(
        IAuthorizedRequest<TResponse> authorizedRequest,
        ClaimsPrincipal user,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorizedRequest.PolicyName))
            return await next(cancellationToken).ConfigureAwait(false);

        AuthorizationResult result;
        if (authorizedRequest is IAuthorizedResourceRequest<TResponse> resourceRequest)
        {
            var resource = await ResolveResourceAsync(resourceRequest, cancellationToken).ConfigureAwait(false);
            result = await provider.AuthorizeAsync(user, authorizedRequest.PolicyName, resource).ConfigureAwait(false);
        }
        else
        {
            result = await provider.AuthorizeAsync(user, authorizedRequest.PolicyName).ConfigureAwait(false);
        }

        if (result.Succeeded)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var errors = result.Failure?.FailureReasons
            .Select(reason => reason.Message)
            .ToArray() ?? ["Authorization failed."];

        throw new AuthorizationFailedException(authorizedRequest.PolicyName, errors);
    }

    private async Task<object?> ResolveResourceAsync(IAuthorizedResourceRequest<TResponse> resourceRequest, CancellationToken cancellationToken)
    {
        // Use reflection instead of dynamic to support internal types
        var resourceProperty = resourceRequest.GetType().GetProperty("Resource");
        var resource = resourceProperty?.GetValue(resourceRequest);

        if (resource != null)
            return resource;

        var requestType = resourceRequest.GetType();
        var resourceType = GetResourceType(requestType);
        if (resourceType == null)
            return null;

        var resolverType = typeof(IResourceResolver<,>).MakeGenericType(requestType, resourceType);
        var resolver = serviceProvider.GetService(resolverType);

        if (resolver == null)
            return null;

        var resolveMethod = resolverType.GetMethod(nameof(IResourceResolver<object, object>.ResolveAsync));
        var task = (Task)resolveMethod!.Invoke(resolver, [resourceRequest, cancellationToken])!;
        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result");
        var resolvedResource = resultProperty!.GetValue(task);

        if (resolvedResource != null)
        {
            var resourceProp = resourceRequest.GetType().GetProperty(nameof(IAuthorizedResourceRequest<object, object>.Resource));
            resourceProp?.SetValue(resourceRequest, resolvedResource);
        }

        return resolvedResource;
    }

    private async Task<object?> ResolveResourceUsingAttribute(
        TRequest request,
        ResolveResourceAttribute resolveResourceAttribute,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest);
        var resolverType = resolveResourceAttribute.ResolverType;

        var resolverInterface = resolverType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                i.GetGenericTypeDefinition() == typeof(IResourceResolver<,>) &&
                                i.GetGenericArguments()[0] == requestType);

        if (resolverInterface == null)
        {
            throw new InvalidOperationException($"Resolver type {resolverType.Name} does not implement IResourceResolver<{requestType.Name}, TResource>");
        }

        var resourceType = resolverInterface.GetGenericArguments()[1];
        var resolver = serviceProvider.GetService(resolverType);

        if (resolver == null)
        {
            throw new InvalidOperationException($"Resource resolver of type {resolverType.Name} is not registered in the service provider");
        }

        var resolveMethod = resolverInterface.GetMethod(nameof(IResourceResolver<object, object>.ResolveAsync));
        var task = (Task)resolveMethod!.Invoke(resolver, [request, cancellationToken])!;
        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result");
        var resolvedResource = resultProperty!.GetValue(task);

        if (resolvedResource != null)
        {
            var resourceProperty = requestType.GetProperty("Resource");
            if (resourceProperty != null && resourceProperty.CanWrite &&
                resourceProperty.PropertyType.IsAssignableFrom(resourceType))
            {
                resourceProperty.SetValue(request, resolvedResource);
            }
        }

        return resolvedResource;
    }

    private static Type? GetResourceType(Type requestType)
    {
        var resourceInterface = requestType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                i.GetGenericTypeDefinition() == typeof(IAuthorizedResourceRequest<,>));

        return resourceInterface?.GetGenericArguments()[0];
    }
}
