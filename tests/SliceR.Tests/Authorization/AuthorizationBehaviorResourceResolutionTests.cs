using System.Security.Claims;
using FakeItEasy;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using SliceR.Authorization;
using Xunit;

namespace SliceR.Tests.Authorization;

public class AuthorizationBehaviorResourceResolutionTests
{
    public record TestDocument(Guid Id, string Content);
    
    public record RequestWithoutResourceInterface : IAuthorizedRequest<Unit>
    {
        public string PolicyName => "test.policy";
    }
    
    public record RequestWithMalformedResourceInterface : IAuthorizedRequest<Unit>
    {
        public string PolicyName => "test.policy";
    }
    
    public record RequestWithResourceButNoGenericInterface : IAuthorizedResourceRequest<Unit>
    {
        public string PolicyName => "test.policy";
        public object? Resource { get; set; } // This will make the dynamic access work but GetResourceType will return null
    }
    
    public record ValidResourceRequest : IAuthorizedResourceRequest<TestDocument, Unit>
    {
        public string PolicyName => "test.policy";
        public TestDocument? Resource { get; set; }
    }
    
    private readonly RequestHandlerDelegate<Unit> _nextMock = _ => Task.FromResult(Unit.Value);
    private readonly AuthorizationResult _successResult = AuthorizationResult.Success();
    
    [Fact]
    public async Task Handle_WithResourceRequestButNoResourceType_ReturnsNullResource()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, A<string>._, A<object>._))
            .Returns(_successResult);
            
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth"))
        };
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(httpContext);
        
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        
        var behavior = new AuthorizationBehavior<RequestWithResourceButNoGenericInterface, Unit>(
            authProvider, httpContextAccessor, serviceProvider);
        var request = new RequestWithResourceButNoGenericInterface();
        
        // Act
        await behavior.Handle(request, _nextMock, CancellationToken.None);
        
        // Assert
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "test.policy", null))
            .MustHaveHappenedOnceExactly();
    }
    
    [Fact]
    public async Task Handle_WithResourceRequestButNoResolver_ReturnsNullResource()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, A<string>._, A<object>._))
            .Returns(_successResult);
            
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth"))
        };
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(httpContext);
        
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        
        var behavior = new AuthorizationBehavior<ValidResourceRequest, Unit>(
            authProvider, httpContextAccessor, serviceProvider);
        var request = new ValidResourceRequest();
        
        // Act
        await behavior.Handle(request, _nextMock, CancellationToken.None);
        
        // Assert
        request.Resource.Should().Be(null);
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "test.policy", null))
            .MustHaveHappenedOnceExactly();
    }
    
    [Fact]
    public async Task Handle_WithResourceRequestAndResolverSetsResource_SetsResourceProperty()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, A<string>._, A<object>._))
            .Returns(_successResult);
            
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth"))
        };
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(httpContext);
        
        var expectedDocument = new TestDocument(Guid.NewGuid(), "Test Content");
        var resolver = A.Fake<IResourceResolver<ValidResourceRequest, TestDocument>>();
        A.CallTo(() => resolver.ResolveAsync(A<ValidResourceRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult<TestDocument?>(expectedDocument));
        
        var services = new ServiceCollection();
        services.AddSingleton(resolver);
        var serviceProvider = services.BuildServiceProvider();
        
        var behavior = new AuthorizationBehavior<ValidResourceRequest, Unit>(
            authProvider, httpContextAccessor, serviceProvider);
        var request = new ValidResourceRequest();
        
        // Act
        await behavior.Handle(request, _nextMock, CancellationToken.None);
        
        // Assert
        request.Resource.Should().Be(expectedDocument);
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "test.policy", expectedDocument))
            .MustHaveHappenedOnceExactly();
    }
    
    [Fact]
    public async Task Handle_WithResourceRequestAndExistingResource_DoesNotCallResolver()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, A<string>._, A<object>._))
            .Returns(_successResult);
            
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth"))
        };
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(httpContext);
        
        var existingDocument = new TestDocument(Guid.NewGuid(), "Existing Content");
        var resolver = A.Fake<IResourceResolver<ValidResourceRequest, TestDocument>>();
        
        var services = new ServiceCollection();
        services.AddSingleton(resolver);
        var serviceProvider = services.BuildServiceProvider();
        
        var behavior = new AuthorizationBehavior<ValidResourceRequest, Unit>(
            authProvider, httpContextAccessor, serviceProvider);
        var request = new ValidResourceRequest { Resource = existingDocument };
        
        // Act
        await behavior.Handle(request, _nextMock, CancellationToken.None);
        
        // Assert
        request.Resource.Should().Be(existingDocument);
        A.CallTo(() => resolver.ResolveAsync(A<ValidResourceRequest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "test.policy", existingDocument))
            .MustHaveHappenedOnceExactly();
    }
    
    [Fact]
    public async Task Handle_WithResourceRequestAndResolverReturnsNull_KeepsResourceAsNull()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, A<string>._, A<object>._))
            .Returns(_successResult);
            
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth"))
        };
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(httpContext);
        
        var resolver = A.Fake<IResourceResolver<ValidResourceRequest, TestDocument>>();
        A.CallTo(() => resolver.ResolveAsync(A<ValidResourceRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult<TestDocument?>(null));
        
        var services = new ServiceCollection();
        services.AddSingleton(resolver);
        var serviceProvider = services.BuildServiceProvider();
        
        var behavior = new AuthorizationBehavior<ValidResourceRequest, Unit>(
            authProvider, httpContextAccessor, serviceProvider);
        var request = new ValidResourceRequest();
        
        // Act
        await behavior.Handle(request, _nextMock, CancellationToken.None);
        
        // Assert
        request.Resource.Should().Be(null);
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "test.policy", null))
            .MustHaveHappenedOnceExactly();
    }
}