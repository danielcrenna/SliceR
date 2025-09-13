using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using SliceR.Validation;
using Xunit;

namespace SliceR.Tests.Validation;

public class ValidationExceptionFilterTests
{
    [Fact]
    public void OnException_WithValidationException_SetsBadRequestResult()
    {
        // Arrange
        var filter = new ValidationExceptionFilter();
        var failures = new[]
        {
            new ValidationFailure("Property1", "Error message 1"),
            new ValidationFailure("Property2", "Error message 2")
        };
        var exception = new ValidationException("Validation failed", failures);

        var actionContext = new ActionContext
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData(),
            ActionDescriptor = new ActionDescriptor()
        };

        var exceptionContext = new ExceptionContext(actionContext, [])
        {
            Exception = exception
        };

        // Act
        filter.OnException(exceptionContext);

        // Assert
        exceptionContext.ExceptionHandled.Should().Be(true);
        var result = exceptionContext.Result as BadRequestObjectResult;
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var errors = result.Value as ValidationProblemDetails;
        errors.Should().NotBeNull();
        errors!.Errors.Should().HaveCount(2);
        errors.Errors.Should().ContainKey("Property1");
        errors.Errors.Should().ContainKey("Property2");
        Assert.Contains("Error message 1", errors.Errors["Property1"]);
        Assert.Contains("Error message 2", errors.Errors["Property2"]);
    }

    [Fact]
    public void OnException_WithNonValidationException_DoesNotHandle()
    {
        // Arrange
        var filter = new ValidationExceptionFilter();
        var exception = new ArgumentException("Some other error");

        var actionContext = new ActionContext
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData(),
            ActionDescriptor = new ActionDescriptor()
        };

        var exceptionContext = new ExceptionContext(actionContext, [])
        {
            Exception = exception
        };

        // Act
        filter.OnException(exceptionContext);

        // Assert
        exceptionContext.ExceptionHandled.Should().Be(false);
        Assert.Null(exceptionContext.Result);
    }

    [Fact]
    public void OnException_WithValidationExceptionWithoutFailures_SetsBadRequestResult()
    {
        // Arrange
        var filter = new ValidationExceptionFilter();
        var exception = new ValidationException("Validation failed", []);

        var actionContext = new ActionContext
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData(),
            ActionDescriptor = new ActionDescriptor()
        };

        var exceptionContext = new ExceptionContext(actionContext, [])
        {
            Exception = exception
        };

        // Act
        filter.OnException(exceptionContext);

        // Assert
        exceptionContext.ExceptionHandled.Should().Be(true);
        var result = exceptionContext.Result as BadRequestObjectResult;
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var errors = result.Value as ValidationProblemDetails;
        errors.Should().NotBeNull();
        errors!.Errors.Should().BeEmpty();
    }
}
