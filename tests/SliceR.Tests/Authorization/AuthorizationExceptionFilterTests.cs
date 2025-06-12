using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using SliceR.Authorization;
using Xunit;

namespace SliceR.Tests.Authorization;

public class AuthorizationExceptionFilterTests
{
    [Fact]
    public void OnException_WithAuthorizationFailedException_SetsForbiddenResult()
    {
        // Arrange
        var filter = new AuthorizationExceptionFilter();
        var errors = new[] { "Error 1", "Error 2" };
        var exception = new AuthorizationFailedException("test-policy", errors);
        
        var actionContext = new ActionContext
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData(),
            ActionDescriptor = new ActionDescriptor()
        };
        
        var exceptionContext = new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = exception
        };
        
        // Act
        filter.OnException(exceptionContext);
        
        // Assert
        exceptionContext.ExceptionHandled.Should().Be(true);
        var result = exceptionContext.Result as ObjectResult;
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        
        var problemDetails = result.Value as ProblemDetails;
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problemDetails.Title.Should().Be("Authorization Failed");
        problemDetails.Detail.Should().Contain("test-policy");
        Assert.Contains("errors", problemDetails.Extensions.Keys);
    }
    
    [Fact]
    public void OnException_WithAuthenticationFailedException_SetsUnauthorizedResult()
    {
        // Arrange
        var filter = new AuthorizationExceptionFilter();
        var errors = new[] { "Not authenticated" };
        var exception = new AuthorizationFailedException("Authentication", errors);
        
        var actionContext = new ActionContext
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData(),
            ActionDescriptor = new ActionDescriptor()
        };
        
        var exceptionContext = new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = exception
        };
        
        // Act
        filter.OnException(exceptionContext);
        
        // Assert
        exceptionContext.ExceptionHandled.Should().Be(true);
        var result = exceptionContext.Result as ObjectResult;
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        
        var problemDetails = result.Value as ProblemDetails;
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problemDetails.Title.Should().Be("Authentication Failed");
    }
    
    [Fact]
    public void OnException_WithNonAuthorizationException_DoesNotHandle()
    {
        // Arrange
        var filter = new AuthorizationExceptionFilter();
        var exception = new ArgumentException("Some other error");
        
        var actionContext = new ActionContext
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData(),
            ActionDescriptor = new ActionDescriptor()
        };
        
        var exceptionContext = new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = exception
        };
        
        // Act
        filter.OnException(exceptionContext);
        
        // Assert
        exceptionContext.ExceptionHandled.Should().Be(false);
        Assert.Null(exceptionContext.Result);
    }
}