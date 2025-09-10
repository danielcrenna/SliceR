using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MediatR;

namespace SliceR.Authorization;

/// <summary>
/// Pipeline behavior that handles authorization for requests that implement IAuthorizedRequest.
/// Supports both policy-based authorization and authentication-only scenarios.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class AuthorizationBehavior<TRequest, TResponse>(
    IAuthorizationProvider provider,
    IHttpContextAccessor accessor,
    IServiceProvider serviceProvider)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuthorizedRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var user = accessor.HttpContext?.User;

        if (user == null || !user.Identity?.IsAuthenticated == true)
        {
            throw new AuthorizationFailedException("Authentication", ["User is not authenticated."]);
        }

        if (string.IsNullOrEmpty(request.PolicyName))
	        return await next(cancellationToken);

        AuthorizationResult result;
        if (request is IAuthorizedResourceRequest<TResponse> resourceRequest)
        {
            var resource = await ResolveResourceAsync(resourceRequest, cancellationToken);
            result = await provider.AuthorizeAsync(user, request.PolicyName, resource);
        }
        else
        {
            result = await provider.AuthorizeAsync(user, request.PolicyName);
        }

        if (result.Succeeded)
        {
            return await next(cancellationToken);
        }

        var errors = result.Failure?.FailureReasons
            .Select(reason => reason.Message)
            .ToArray() ?? ["Authorization failed."];

        throw new AuthorizationFailedException(request.PolicyName, errors);
    }

    private async Task<object?> ResolveResourceAsync(IAuthorizedResourceRequest<TResponse> resourceRequest, CancellationToken cancellationToken)
    {
        dynamic dynamicRequest = resourceRequest;
        var resource = dynamicRequest.Resource;
        
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
        await task;

        var resultProperty = task.GetType().GetProperty("Result");
        var resolvedResource = resultProperty!.GetValue(task);

        if (resolvedResource != null)
        {
            var resourceProperty = resourceRequest.GetType().GetProperty(nameof(IAuthorizedResourceRequest<object, object>.Resource));
            resourceProperty?.SetValue(resourceRequest, resolvedResource);
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