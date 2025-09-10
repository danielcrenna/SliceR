using System.Reflection;
using System.Security.Claims;
using FakeItEasy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using SliceR.Authorization;
using Xunit;

namespace SliceR.Tests.Authorization;

public class AuthorizationBehaviorReflectionEdgeCasesTests
{
    public record TestDocument(Guid Id, string Content);
    
    public record ValidResourceRequest : IAuthorizedResourceRequest<TestDocument, Unit>
    {
        public string PolicyName => "test.policy";
        public TestDocument? Resource { get; set; }
    }
    
    public class BrokenResourceResolver : IResourceResolver<ValidResourceRequest, TestDocument>
    {
        public Task<TestDocument?> ResolveAsync(ValidResourceRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Resolver failed");
        }
    }
    
    private readonly RequestHandlerDelegate<Unit> _nextMock = _ => Task.FromResult(Unit.Value);
    private readonly AuthorizationResult _successResult = AuthorizationResult.Success();
    
    [Fact]
    public async Task Handle_WithResolverThatThrowsException_PropagatesException()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth"))
        };
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(httpContext);
        
        var services = new ServiceCollection();
        services.AddSingleton<IResourceResolver<ValidResourceRequest, TestDocument>, BrokenResourceResolver>();
        var serviceProvider = services.BuildServiceProvider();
        
        var behavior = new AuthorizationBehavior<ValidResourceRequest, Unit>(
            authProvider, httpContextAccessor, serviceProvider);
        var request = new ValidResourceRequest();
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<TargetInvocationException>(() =>
            behavior.Handle(request, _nextMock, CancellationToken.None));
        
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.InnerException!.Message.Should().Be("Resolver failed");
    }
    
    [Fact]
    public async Task Handle_WithResolverAndCancellationToken_PassesCancellationTokenCorrectly()
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
            .Returns(Task.FromResult<TestDocument?>(new TestDocument(Guid.NewGuid(), "Test")));
        
        var services = new ServiceCollection();
        services.AddSingleton(resolver);
        var serviceProvider = services.BuildServiceProvider();
        
        var behavior = new AuthorizationBehavior<ValidResourceRequest, Unit>(
            authProvider, httpContextAccessor, serviceProvider);
        var request = new ValidResourceRequest();
        var cancellationToken = new CancellationToken();
        
        // Act
        await behavior.Handle(request, _nextMock, cancellationToken);
        
        // Assert
        A.CallTo(() => resolver.ResolveAsync(request, cancellationToken))
            .MustHaveHappenedOnceExactly();
    }
}