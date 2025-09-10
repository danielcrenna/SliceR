using System.Security.Claims;
using FakeItEasy;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using SliceR.Authorization;
using Xunit;

namespace SliceR.Tests.Authorization;

public class ResourceResolverTests
{
    public record TestDocument(Guid Id, string Content);
    
    public record UpdateDocumentCommand : IAuthorizedResourceRequest<TestDocument, Unit>
    {
        public Guid DocumentId { get; init; }
        public string? NewContent { get; init; }
        public string PolicyName => "documents.update";
        public TestDocument? Resource { get; set; }
    }
    
    public record UpdateDocumentWithResourceCommand : IAuthorizedResourceRequest<TestDocument, Unit>
    {
        public Guid DocumentId { get; init; }
        public string? NewContent { get; init; }
        public string PolicyName => "documents.update";
        public TestDocument? Resource { get; set; } = new(Guid.NewGuid(), "Existing");
    }
    
    public class TestDocumentResolver : IResourceResolver<UpdateDocumentCommand, TestDocument>
    {
        public Task<TestDocument?> ResolveAsync(UpdateDocumentCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult<TestDocument?>(new TestDocument(request.DocumentId, "Resolved Content"));
        }
    }
    
    private readonly RequestHandlerDelegate<Unit> _nextMock = _ => Task.FromResult(Unit.Value);
    private readonly AuthorizationResult _successResult = AuthorizationResult.Success();
    
    [Fact]
    public async Task Handle_WithResourceRequestAndNoExistingResource_ResolvesResource()
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
        services.AddTransient<IResourceResolver<UpdateDocumentCommand, TestDocument>, TestDocumentResolver>();
        var serviceProvider = services.BuildServiceProvider();
        
        var behavior = new AuthorizationBehavior<UpdateDocumentCommand, Unit>(
            authProvider, httpContextAccessor, serviceProvider);
        var request = new UpdateDocumentCommand { DocumentId = Guid.NewGuid(), NewContent = "New Content" };
        
        // Act
        await behavior.Handle(request, _nextMock, CancellationToken.None);
        
        // Assert
        request.Resource.Should().NotBeNull();
        request.Resource!.Id.Should().Be(request.DocumentId);
        request.Resource.Content.Should().Be("Resolved Content");
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "documents.update", A<TestDocument>._))
            .MustHaveHappenedOnceExactly();
    }
    
    [Fact]
    public async Task Handle_WithResourceRequestAndExistingResource_DoesNotResolveResource()
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
        services.AddTransient<IResourceResolver<UpdateDocumentCommand, TestDocument>, TestDocumentResolver>();
        var serviceProvider = services.BuildServiceProvider();
        
        var behavior = new AuthorizationBehavior<UpdateDocumentWithResourceCommand, Unit>(
            authProvider, httpContextAccessor, serviceProvider);
        var request = new UpdateDocumentWithResourceCommand { DocumentId = Guid.NewGuid(), NewContent = "New Content" };
        var originalResource = request.Resource;
        
        // Act
        await behavior.Handle(request, _nextMock, CancellationToken.None);
        
        // Assert
        request.Resource.Should().Be(originalResource);
        request.Resource!.Content.Should().Be("Existing");
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "documents.update", originalResource))
            .MustHaveHappenedOnceExactly();
    }
    
    [Fact]
    public async Task Handle_WithResourceRequestAndNoResolver_UsesNullResource()
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
        
        var behavior = new AuthorizationBehavior<UpdateDocumentCommand, Unit>(
            authProvider, httpContextAccessor, serviceProvider);
        var request = new UpdateDocumentCommand { DocumentId = Guid.NewGuid(), NewContent = "New Content" };
        
        // Act
        await behavior.Handle(request, _nextMock, CancellationToken.None);
        
        // Assert
        request.Resource.Should().Be(null);
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "documents.update", null))
            .MustHaveHappenedOnceExactly();
    }
    
    public class NullReturningResolver : IResourceResolver<UpdateDocumentCommand, TestDocument>
    {
        public Task<TestDocument?> ResolveAsync(UpdateDocumentCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult<TestDocument?>(null);
        }
    }
    
    [Fact]
    public async Task Handle_WithResourceRequestAndResolverReturnsNull_UsesNullResource()
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
        services.AddTransient<IResourceResolver<UpdateDocumentCommand, TestDocument>, NullReturningResolver>();
        var serviceProvider = services.BuildServiceProvider();
        
        var behavior = new AuthorizationBehavior<UpdateDocumentCommand, Unit>(
            authProvider, httpContextAccessor, serviceProvider);
        var request = new UpdateDocumentCommand { DocumentId = Guid.NewGuid(), NewContent = "New Content" };
        
        // Act
        await behavior.Handle(request, _nextMock, CancellationToken.None);
        
        // Assert
        request.Resource.Should().Be(null);
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "documents.update", null))
            .MustHaveHappenedOnceExactly();
    }
    
    public class AsyncResourceResolver : IResourceResolver<UpdateDocumentCommand, TestDocument>
    {
        public async Task<TestDocument?> ResolveAsync(UpdateDocumentCommand request, CancellationToken cancellationToken)
        {
            await Task.Delay(10, cancellationToken);
            return new TestDocument(request.DocumentId, "Async Resolved");
        }
    }
    
    [Fact]
    public async Task Handle_WithAsyncResourceResolver_ResolvesResourceAsynchronously()
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
        services.AddTransient<IResourceResolver<UpdateDocumentCommand, TestDocument>, AsyncResourceResolver>();
        var serviceProvider = services.BuildServiceProvider();
        
        var behavior = new AuthorizationBehavior<UpdateDocumentCommand, Unit>(
            authProvider, httpContextAccessor, serviceProvider);
        var request = new UpdateDocumentCommand { DocumentId = Guid.NewGuid(), NewContent = "New Content" };
        
        // Act
        await behavior.Handle(request, _nextMock, CancellationToken.None);
        
        // Assert
        request.Resource.Should().NotBeNull();
        request.Resource!.Content.Should().Be("Async Resolved");
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "documents.update", A<TestDocument>._))
            .MustHaveHappenedOnceExactly();
    }
    
    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokResourceResolver()
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
        
        var resolver = A.Fake<IResourceResolver<UpdateDocumentCommand, TestDocument>>();
        A.CallTo(() => resolver.ResolveAsync(A<UpdateDocumentCommand>._, A<CancellationToken>._))
            .Returns(Task.FromResult<TestDocument?>(new TestDocument(Guid.NewGuid(), "Test")));
            
        var services = new ServiceCollection();
        services.AddSingleton(resolver);
        var serviceProvider = services.BuildServiceProvider();
        
        var behavior = new AuthorizationBehavior<UpdateDocumentCommand, Unit>(
            authProvider, httpContextAccessor, serviceProvider);
        var request = new UpdateDocumentCommand { DocumentId = Guid.NewGuid(), NewContent = "New Content" };
        var cancellationToken = new CancellationToken();
        
        // Act
        await behavior.Handle(request, _nextMock, cancellationToken);
        
        // Assert
        A.CallTo(() => resolver.ResolveAsync(request, cancellationToken))
            .MustHaveHappenedOnceExactly();
    }
}