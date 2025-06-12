using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using DefaultAuthorizationMiddlewareResultHandler = Microsoft.AspNetCore.Authorization.Policy.AuthorizationMiddlewareResultHandler;

namespace SliceR.Authorization;

internal class AuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly DefaultAuthorizationMiddlewareResultHandler _inner = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult is { Forbidden: true, AuthorizationFailure: not null })
        {
            var errors = authorizeResult.AuthorizationFailure.FailureReasons
                .Select(reason => reason.Message)
                .ToArray();

            var problemDetails = new ValidationProblemDetails
            {
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "You do not have permission to perform this action.",
                Errors = new Dictionary<string, string[]> { { "permission_failures", errors } }
            };

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(problemDetails);
            return;
        }
        
        await CallInnerHandlerAsync(next, context, policy, authorizeResult);
    }
    
    protected virtual Task CallInnerHandlerAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        return _inner.HandleAsync(next, context, policy, authorizeResult);
    }
}