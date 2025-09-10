using System.Security.Claims;
using FakeItEasy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using SliceR.Authorization;
using Xunit;

namespace SliceR.Tests.Authorization;

public class AuthenticatedRequestTests
{
    public record TestAuthenticatedRequest : IAuthenticatedRequest<string>
    {
    }
    
    private readonly RequestHandlerDelegate<string> _nextMock = _ => Task.FromResult("Success");
    
    [Fact]
    public void IAuthenticatedRequest_PolicyName_ReturnsNull()
    {
        // Arrange
        var request = new TestAuthenticatedRequest();
        
        // Act & Assert
        ((IAuthorizedRequest<string>)request).PolicyName.Should().Be(null);
    }
    
    [Fact]
    public async Task AuthorizationBehavior_WithAuthenticatedRequestAndNoUser_ThrowsAuthorizationFailedException()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(null);
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var behavior = new AuthorizationBehavior<TestAuthenticatedRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestAuthenticatedRequest();
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<AuthorizationFailedException>(() => 
            behavior.Handle(request, _nextMock, CancellationToken.None));
            
        exception.FailedRequirement.Should().Be("Authentication");
        exception.Errors.Should().Contain("User is not authenticated.");
    }
    
    [Fact]
    public async Task AuthorizationBehavior_WithAuthenticatedRequestAndUnauthenticatedUser_ThrowsAuthorizationFailedException()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(httpContext);
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var behavior = new AuthorizationBehavior<TestAuthenticatedRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestAuthenticatedRequest();
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<AuthorizationFailedException>(() => 
            behavior.Handle(request, _nextMock, CancellationToken.None));
            
        exception.FailedRequirement.Should().Be("Authentication");
        exception.Errors.Should().Contain("User is not authenticated.");
    }
    
    [Fact]
    public async Task AuthorizationBehavior_WithAuthenticatedRequestAndAuthenticatedUser_CallsNext()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth"))
        };
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(httpContext);
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var behavior = new AuthorizationBehavior<TestAuthenticatedRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestAuthenticatedRequest();
        
        // Act
        var result = await behavior.Handle(request, _nextMock, CancellationToken.None);
        
        // Assert
        result.Should().Be("Success");
        A.CallTo(authProvider).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task AuthorizationBehavior_WithAuthenticatedRequestAndMultipleClaims_CallsNext()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, "TestUser"));
        identity.AddClaim(new Claim(ClaimTypes.Email, "test@example.com"));
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(httpContext);
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var behavior = new AuthorizationBehavior<TestAuthenticatedRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestAuthenticatedRequest();
        
        // Act
        var result = await behavior.Handle(request, _nextMock, CancellationToken.None);
        
        // Assert
        result.Should().Be("Success");
        A.CallTo(authProvider).MustNotHaveHappened();
    }
}