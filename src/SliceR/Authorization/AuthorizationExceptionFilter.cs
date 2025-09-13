using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SliceR.Authorization;

internal sealed class AuthorizationExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AuthorizationFailedException exception)
            return;

        var statusCode = exception.FailedRequirement == "Authentication"
            ? StatusCodes.Status401Unauthorized
            : StatusCodes.Status403Forbidden;

        var title = exception.FailedRequirement == "Authentication"
            ? "Authentication Failed"
            : "Authorization Failed";

        var problemDetails = new ProblemDetails
        {
            Title = title,
            Status = statusCode,
            Detail = $"Failed requirement: {exception.FailedRequirement}",
            Extensions =
            {
                ["errors"] = exception.Errors
            }
        };

        context.Result = new ObjectResult(problemDetails) { StatusCode = statusCode };
        context.ExceptionHandled = true;
    }
}
