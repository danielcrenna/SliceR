using System.Security.Claims;
using FakeItEasy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using SliceR.Authorization;
using Xunit;

namespace SliceR.Tests.Authorization;

public sealed class AttributeAuthorizationBehaviorTests
{
    #region Test Request Classes

    [Authorized("test-policy")]
    internal sealed record TestAuthorizeOnlyRequest : IRequest<string>;

    [Authenticated]
    internal sealed record TestAuthenticatedOnlyRequest : IRequest<string>;

    [Authorized]
    internal sealed record TestAuthorizedNoPolicy : IRequest<string>;

    [Authorized("resource-policy")]
    [ResolveResource(typeof(TestResourceResolver))]
    internal sealed record TestAuthorizeWithResourceRequest : IRequest<string>
    {
        public Guid ResourceId { get; init; }
        public TestResource? Resource { get; set; }
    }

    internal sealed record TestNoAuthorizationRequest : IRequest<string>;

    internal sealed record TestInterfaceBasedRequest : IAuthorizedRequest<string>
    {
        public string? PolicyName { get; init; }
    }

    internal sealed record TestInterfaceBasedResourceRequest : IAuthorizedResourceRequest<TestResource, string>
    {
        public string? PolicyName { get; init; }
        public TestResource? Resource { get; set; }
    }

    internal sealed class TestResource
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    internal sealed class TestResourceResolver : IResourceResolver<TestAuthorizeWithResourceRequest, TestResource>
    {
        public Task<TestResource?> ResolveAsync(TestAuthorizeWithResourceRequest request, CancellationToken cancellationToken) => Task.FromResult<TestResource?>(new TestResource { Id = request.ResourceId, Name = "Test Resource" });
    }

    internal sealed class InvalidResolver;

    #endregion

    private readonly RequestHandlerDelegate<string> _nextMock = _ => Task.FromResult("Success");
    private readonly AuthorizationResult _successResult = AuthorizationResult.Success();
    private readonly AuthorizationResult _failedResult;

    public AttributeAuthorizationBehaviorTests()
    {
        var authFailure = AuthorizationFailure.Failed([new AuthorizationFailureReason(A.Fake<IAuthorizationHandler>(), "Test error message")]);
        _failedResult = AuthorizationResult.Failed(authFailure);
    }

    [Fact]
    public async Task Handle_WithAuthorizedAttributeOnly_AuthorizesSuccessfully()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "test-policy", null))
            .Returns(_successResult);

        var httpContextAccessor = CreateAuthenticatedContextAccessor();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var behavior = new AuthorizationBehavior<TestAuthorizeOnlyRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestAuthorizeOnlyRequest();

        // Act
        var result = await behavior.Handle(request, _nextMock, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Equal("Success", result);
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "test-policy", null))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_WithAuthorizedAttributeOnly_AuthorizationFails_ThrowsException()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "test-policy", null))
            .Returns(_failedResult);

        var httpContextAccessor = CreateAuthenticatedContextAccessor();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var behavior = new AuthorizationBehavior<TestAuthorizeOnlyRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestAuthorizeOnlyRequest();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AuthorizationFailedException>(
            () => behavior.Handle(request, _nextMock, CancellationToken.None)).ConfigureAwait(false);

        Assert.Equal("test-policy", exception.FailedRequirement);
        Assert.Contains("Test error message", exception.Errors);
    }

    [Fact]
    public async Task Handle_WithBothAttributes_ResolvesResourceAndAuthorizes()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "resource-policy", A<TestResource>._))
            .Returns(_successResult);

        var httpContextAccessor = CreateAuthenticatedContextAccessor();

        var services = new ServiceCollection();
        services.AddSingleton<TestResourceResolver>();
        var serviceProvider = services.BuildServiceProvider();

        var behavior = new AuthorizationBehavior<TestAuthorizeWithResourceRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestAuthorizeWithResourceRequest { ResourceId = Guid.NewGuid() };

        // Act
        var result = await behavior.Handle(request, _nextMock, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Equal("Success", result);
        Assert.NotNull(request.Resource);
        Assert.Equal(request.ResourceId, request.Resource.Id);
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "resource-policy", A<TestResource>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_WithNoAttributesAndNoInterfaces_PassesThrough()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        var httpContextAccessor = CreateAuthenticatedContextAccessor();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var behavior = new AuthorizationBehavior<TestNoAuthorizationRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestNoAuthorizationRequest();

        // Act
        var result = await behavior.Handle(request, _nextMock, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Equal("Success", result);
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, A<string>._, A<object?>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_WithInterfaceBasedRequest_WorksCorrectly()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "interface-policy", null))
            .Returns(_successResult);

        var httpContextAccessor = CreateAuthenticatedContextAccessor();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var behavior = new AuthorizationBehavior<TestInterfaceBasedRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestInterfaceBasedRequest { PolicyName = "interface-policy" };

        // Act
        var result = await behavior.Handle(request, _nextMock, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Equal("Success", result);
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "interface-policy", null))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_WithInterfaceBasedResourceRequest_WorksCorrectly()
    {
        // Arrange
        var resource = new TestResource { Id = Guid.NewGuid(), Name = "Interface Resource" };
        var authProvider = A.Fake<IAuthorizationProvider>();
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "interface-resource-policy", resource))
            .Returns(_successResult);

        var httpContextAccessor = CreateAuthenticatedContextAccessor();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var behavior = new AuthorizationBehavior<TestInterfaceBasedResourceRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestInterfaceBasedResourceRequest
        {
            PolicyName = "interface-resource-policy",
            Resource = resource
        };

        // Act
        var result = await behavior.Handle(request, _nextMock, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Equal("Success", result);
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, "interface-resource-policy", resource))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_WithUnauthenticatedUser_ThrowsAuthorizationFailedException()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(null);

        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var behavior = new AuthorizationBehavior<TestAuthorizeOnlyRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestAuthorizeOnlyRequest();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AuthorizationFailedException>(
            () => behavior.Handle(request, _nextMock, CancellationToken.None)).ConfigureAwait(false);

        Assert.Equal("Authentication", exception.FailedRequirement);
        Assert.Contains("User is not authenticated.", exception.Errors);
    }

    [Fact]
    public async Task Handle_WithResolveResourceButResolverNotRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        var httpContextAccessor = CreateAuthenticatedContextAccessor();
        var serviceProvider = new ServiceCollection().BuildServiceProvider(); // Resolver not registered

        var behavior = new AuthorizationBehavior<TestAuthorizeWithResourceRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestAuthorizeWithResourceRequest { ResourceId = Guid.NewGuid() };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(request, _nextMock, CancellationToken.None)).ConfigureAwait(false);
    }

    [Fact]
    public void ResolveResourceAttribute_WithInvalidResolverType_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new ResolveResourceAttribute(typeof(InvalidResolver)));

        Assert.Contains("must implement IResourceResolver<,>", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveResourceAttribute_WithNullResolverType_ThrowsArgumentNullException() =>
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new ResolveResourceAttribute(null!));

    [Fact]
    public async Task Handle_WithAuthenticatedAttribute_RequiresAuthenticationOnly()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        var httpContextAccessor = CreateAuthenticatedContextAccessor();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var behavior = new AuthorizationBehavior<TestAuthenticatedOnlyRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestAuthenticatedOnlyRequest();

        // Act
        var result = await behavior.Handle(request, _nextMock, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Equal("Success", result);
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, A<string>._, A<object?>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_WithAuthenticatedAttribute_UnauthenticatedUser_ThrowsException()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(null);

        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var behavior = new AuthorizationBehavior<TestAuthenticatedOnlyRequest, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestAuthenticatedOnlyRequest();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AuthorizationFailedException>(
            () => behavior.Handle(request, _nextMock, CancellationToken.None)).ConfigureAwait(false);

        Assert.Equal("Authentication", exception.FailedRequirement);
        Assert.Contains("User is not authenticated.", exception.Errors);
    }

    [Fact]
    public async Task Handle_WithParameterlessAuthorized_RequiresAuthenticationOnly()
    {
        // Arrange
        var authProvider = A.Fake<IAuthorizationProvider>();
        var httpContextAccessor = CreateAuthenticatedContextAccessor();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var behavior = new AuthorizationBehavior<TestAuthorizedNoPolicy, string>(authProvider, httpContextAccessor, serviceProvider);
        var request = new TestAuthorizedNoPolicy();

        // Act
        var result = await behavior.Handle(request, _nextMock, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Equal("Success", result);
        A.CallTo(() => authProvider.AuthorizeAsync(A<ClaimsPrincipal>._, A<string>._, A<object?>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public void AuthorizedAttribute_WithEmptyString_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new AuthorizedAttribute(""));

        Assert.Contains("Policy name cannot be an empty string", exception.Message, StringComparison.Ordinal);
    }

    private static IHttpContextAccessor CreateAuthenticatedContextAccessor()
    {
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        var httpContext = A.Fake<HttpContext>();
        var user = new ClaimsPrincipal(new ClaimsIdentity("test"));

        A.CallTo(() => httpContextAccessor.HttpContext).Returns(httpContext);
        A.CallTo(() => httpContext.User).Returns(user);

        return httpContextAccessor;
    }
}
