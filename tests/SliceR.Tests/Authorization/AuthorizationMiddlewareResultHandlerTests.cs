using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SliceR.Tests.Authorization;

public class AuthorizationMiddlewareResultHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithSuccessResult_CallsNext()
    {
        // Arrange
        var handler = new SliceR.Authorization.AuthorizationMiddlewareResultHandler();
        var context = new DefaultHttpContext();
        var requirements = new IAuthorizationRequirement[] { new DenyAnonymousAuthorizationRequirement() };
        var policy = new AuthorizationPolicy(requirements, ["TestScheme"]);
        var authResult = PolicyAuthorizationResult.Success();
        var nextCalled = false;

        // Act
        await handler.HandleAsync(Next, context, policy, authResult);
        
        // Assert
        Assert.True(nextCalled);
        return;

        Task Next(HttpContext _)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }
    }
    
    [Fact]
    public async Task HandleAsync_WithForbiddenResult_SetsForbiddenStatusCode()
    {
        // Arrange
        var handler = new SliceR.Authorization.AuthorizationMiddlewareResultHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        
        var requirements = new IAuthorizationRequirement[] { new DenyAnonymousAuthorizationRequirement() };
        var policy = new AuthorizationPolicy(requirements, ["TestScheme"]);
        
        var failureReasons = new List<AuthorizationFailureReason>
        {
            new AuthorizationFailureReason(null!, "Reason 1"),
            new AuthorizationFailureReason(null!, "Reason 2")
        };
        var authFailure = AuthorizationFailure.Failed(failureReasons);
        var authResult = PolicyAuthorizationResult.Forbid(authFailure);
        
        var nextCalled = false;

        // Act
        await handler.HandleAsync(Next, context, policy, authResult);
        
        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled);
        
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseContent = await reader.ReadToEndAsync();
        Assert.Contains("\"status\":403", responseContent);
        Assert.Contains("\"title\":\"Forbidden\"", responseContent);
        Assert.Contains("\"detail\":\"You do not have permission to perform this action.\"", responseContent);
        Assert.Contains("Reason 1", responseContent);
        Assert.Contains("Reason 2", responseContent);
        
        return;

        Task Next(HttpContext _)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }
    }
    
    [Fact]
    public async Task HandleAsync_ForNonHandledCases_CallsInnerHandler()
    {
        // Arrange
        var handler = new SliceR.Authorization.AuthorizationMiddlewareResultHandler();
        var context = new DefaultHttpContext();
        
        // Creating a mock test case that reaches the else branch by setting Forbidden to true
        // but AuthorizationFailure to null which activates the inner handler code path
        var forbidResult = PolicyAuthorizationResult.Forbid();
        var propertyInfo = forbidResult.GetType().GetProperty("AuthorizationFailure", 
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        
        // Setting to null using reflection - our test just needs to trigger the else branch
        if (propertyInfo != null)
        {
            propertyInfo.SetValue(forbidResult, null);
        }
        
        var requirements = new IAuthorizationRequirement[] { new DenyAnonymousAuthorizationRequirement() };
        var policy = new AuthorizationPolicy(requirements, ["TestScheme"]);
        var innerHandlerCalled = false;
        
        // Create a test-specific implementation to avoid authentication service dependencies
        var handlerWithMockInner = new TestAuthorizationMiddlewareResultHandler(
            (n, c, p, r) =>
            {
                innerHandlerCalled = true;
                return Task.CompletedTask;
            });

        // Act
        await handlerWithMockInner.HandleAsync(Next, context, policy, forbidResult);
        
        // Assert
        Assert.True(innerHandlerCalled);
        
        return;

        Task Next(HttpContext _)
        {
            return Task.CompletedTask;
        }
    }
    
    // Test helper class to allow mocking the inner handler
    private class TestAuthorizationMiddlewareResultHandler : SliceR.Authorization.AuthorizationMiddlewareResultHandler
    {
        private readonly Func<RequestDelegate, HttpContext, AuthorizationPolicy, 
            PolicyAuthorizationResult, Task> _innerHandlerAction;
            
        public TestAuthorizationMiddlewareResultHandler(
            Func<RequestDelegate, HttpContext, AuthorizationPolicy, 
                PolicyAuthorizationResult, Task> innerHandlerAction)
        {
            _innerHandlerAction = innerHandlerAction;
        }
        
        protected override Task CallInnerHandlerAsync(
            RequestDelegate next, 
            HttpContext context, 
            AuthorizationPolicy policy, 
            PolicyAuthorizationResult authorizeResult)
        {
            return _innerHandlerAction(next, context, policy, authorizeResult);
        }
    }
}