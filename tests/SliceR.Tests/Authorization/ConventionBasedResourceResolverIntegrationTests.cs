using System.Security.Claims;
using FakeItEasy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using SliceR.Authorization;
using Xunit;

namespace SliceR.Tests.Authorization;

public class ConventionBasedResourceResolverIntegrationTests
{
    public record TestDocument(Guid Id, string Content, string Owner);

    public record UpdateDocumentCommand : IAuthorizedResourceRequest<TestDocument, Unit>
    {
        public Guid DocumentId { get; init; }
        public string? NewContent { get; init; }
        public string PolicyName => "documents.update";
        public TestDocument? Resource { get; set; }
    }

    private readonly RequestHandlerDelegate<Unit> _nextMock = _ => Task.FromResult(Unit.Value);
    private readonly AuthorizationResult _successResult = AuthorizationResult.Success();

    [Fact]
    public async Task AuthorizationBehavior_WithConventionBasedResolver_ResolvesAndAuthorizes()
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
        services.AddSingleton<IDocumentRepository, DocumentRepository>();
        services.WithResourceResolver<UpdateDocumentCommand, TestDocument>(async (request, sp, ct) =>
        {
            var repository = sp.GetRequiredService<IDocumentRepository>();
            return await repository.GetByIdAsync(request.DocumentId);
        });

        var serviceProvider = services.BuildServiceProvider();
        var behavior = new AuthorizationBehavior<UpdateDocumentCommand, Unit>(
            authProvider, httpContextAccessor, serviceProvider);

        var documentId = Guid.NewGuid();
        var request = new UpdateDocumentCommand { DocumentId = documentId, NewContent = "New Content" };

        // Act
        await behavior.Handle(request, _nextMock, CancellationToken.None);

        // Assert
        request.Resource.Should().NotBeNull();
        request.Resource!.Id.Should().Be(documentId);
        request.Resource.Content.Should().Be("Repository Document");
        request.Resource.Owner.Should().Be("TestOwner");

        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "documents.update", A<TestDocument>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AuthorizationBehavior_WithConventionBasedResolverReturningNull_UsesNullForAuthorization()
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
        services.WithResourceResolver<UpdateDocumentCommand, TestDocument>((request, sp, ct) =>
            Task.FromResult<TestDocument?>(null));

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

    [Fact]
    public async Task AuthorizationBehavior_WithMultipleConventionBasedResolvers_UsesCorrectResolver()
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
        services
            .WithResourceResolver<UpdateDocumentCommand, TestDocument>((request, sp, ct) =>
                Task.FromResult<TestDocument?>(new TestDocument(request.DocumentId, "Document Content", "Owner1")))
            .WithResourceResolver<AnotherCommand, TestDocument>((request, sp, ct) =>
                Task.FromResult<TestDocument?>(new TestDocument(request.DocumentId, "Another Content", "Owner2")));

        var serviceProvider = services.BuildServiceProvider();
        var behavior = new AuthorizationBehavior<UpdateDocumentCommand, Unit>(
            authProvider, httpContextAccessor, serviceProvider);

        var request = new UpdateDocumentCommand { DocumentId = Guid.NewGuid(), NewContent = "New Content" };

        // Act
        await behavior.Handle(request, _nextMock, CancellationToken.None);

        // Assert
        request.Resource.Should().NotBeNull();
        request.Resource!.Content.Should().Be("Document Content");
        request.Resource.Owner.Should().Be("Owner1");
    }

    [Fact]
    public async Task AuthorizationBehavior_WithAsyncConventionBasedResolver_HandlesAsyncCorrectly()
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
        services.WithResourceResolver<UpdateDocumentCommand, TestDocument>(async (request, sp, ct) =>
        {
            await Task.Delay(10, ct);
            return new TestDocument(request.DocumentId, "Async Content", "AsyncOwner");
        });

        var serviceProvider = services.BuildServiceProvider();
        var behavior = new AuthorizationBehavior<UpdateDocumentCommand, Unit>(
            authProvider, httpContextAccessor, serviceProvider);

        var request = new UpdateDocumentCommand { DocumentId = Guid.NewGuid(), NewContent = "New Content" };

        // Act
        await behavior.Handle(request, _nextMock, CancellationToken.None);

        // Assert
        request.Resource.Should().NotBeNull();
        request.Resource!.Content.Should().Be("Async Content");
        request.Resource.Owner.Should().Be("AsyncOwner");
    }

    public record AnotherCommand : IAuthorizedResourceRequest<TestDocument, Unit>
    {
        public Guid DocumentId { get; init; }
        public string PolicyName => "another.policy";
        public TestDocument? Resource { get; set; }
    }

    public interface IDocumentRepository
    {
        Task<TestDocument?> GetByIdAsync(Guid id);
    }

    public class DocumentRepository : IDocumentRepository
    {
        public Task<TestDocument?> GetByIdAsync(Guid id) => Task.FromResult<TestDocument?>(new TestDocument(id, "Repository Document", "TestOwner"));
    }
}
