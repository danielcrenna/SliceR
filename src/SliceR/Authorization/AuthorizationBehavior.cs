using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace SliceR.Authorization;

/// <summary>
/// Pipeline behavior that handles authorization for requests that implement IAuthorizedRequest.
/// Supports both policy-based authorization and authentication-only scenarios.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class AuthorizationBehavior<TRequest, TResponse>(
    IAuthorizationProvider provider,
    IHttpContextAccessor accessor)
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
        if (request is IAuthorizedResourceRequest<TResponse> hasResource)
        {
            dynamic body = hasResource;
            result = await provider.AuthorizeAsync(user, request.PolicyName, body.Resource);
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
}