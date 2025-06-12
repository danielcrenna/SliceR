using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SliceR.Validation;

internal sealed class ValidationExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ValidationException exception)
            return;

        var errors = exception.Errors
            .GroupBy(e => string.IsNullOrWhiteSpace(e.PropertyName) ? "permission_failures" : e.PropertyName)
            .ToDictionary(g => g.Key, g =>
                g.Select(e => e.ErrorMessage).ToArray());
        
        var problemDetails = new ValidationProblemDetails(errors)
        {
            Detail = exception.Message,
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        };

        context.Result = new BadRequestObjectResult(problemDetails);
        context.ExceptionHandled = true;
    }
}