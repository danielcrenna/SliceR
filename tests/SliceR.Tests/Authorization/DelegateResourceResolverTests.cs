using MediatR;
using SliceR.Authorization;
using Xunit;

namespace SliceR.Tests.Authorization;

public class DelegateResourceResolverTests
{
    public record TestDocument(Guid Id, string Content);

    public record TestRequest : IAuthorizedResourceRequest<TestDocument, Unit>
    {
        public Guid DocumentId { get; init; }
        public string PolicyName => "test.policy";
        public TestDocument? Resource { get; set; }
    }

    [Fact]
    public async Task ResolveAsync_WithDelegate_CallsDelegate()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var expectedDocument = new TestDocument(Guid.NewGuid(), "Test Content");

        var delegateResolver = new DelegateResourceResolver<TestRequest, TestDocument>(
            (request, sp, ct) => Task.FromResult<TestDocument?>(expectedDocument),
            serviceProvider);

        var request = new TestRequest { DocumentId = Guid.NewGuid() };

        // Act
        var result = await delegateResolver.ResolveAsync(request, CancellationToken.None);

        // Assert
        result.Should().Be(expectedDocument);
    }

    [Fact]
    public async Task ResolveAsync_WithServiceProviderAccess_CanResolveServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var serviceProvider = services.BuildServiceProvider();

        var delegateResolver = new DelegateResourceResolver<TestRequest, TestDocument>(
            (request, sp, ct) =>
            {
                var testService = sp.GetRequiredService<ITestService>();
                return Task.FromResult<TestDocument?>(new TestDocument(request.DocumentId, testService.GetContent()));
            },
            serviceProvider);

        var request = new TestRequest { DocumentId = Guid.NewGuid() };

        // Act
        var result = await delegateResolver.ResolveAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(request.DocumentId);
        result.Content.Should().Be("Service Content");
    }

    [Fact]
    public async Task ResolveAsync_WithCancellationToken_PassesTokenToDelegate()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var cancellationTokenPassed = CancellationToken.None;

        var delegateResolver = new DelegateResourceResolver<TestRequest, TestDocument>(
            (request, sp, ct) =>
            {
                cancellationTokenPassed = ct;
                return Task.FromResult<TestDocument?>(new TestDocument(request.DocumentId, "Test"));
            },
            serviceProvider);

        var request = new TestRequest { DocumentId = Guid.NewGuid() };
        var cancellationToken = CancellationToken.None;

        // Act
        await delegateResolver.ResolveAsync(request, cancellationToken);

        // Assert
        cancellationTokenPassed.Should().Be(cancellationToken);
    }

    [Fact]
    public async Task ResolveAsync_WhenDelegateReturnsNull_ReturnsNull()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var delegateResolver = new DelegateResourceResolver<TestRequest, TestDocument>(
            (request, sp, ct) => Task.FromResult<TestDocument?>(null),
            serviceProvider);

        var request = new TestRequest { DocumentId = Guid.NewGuid() };

        // Act
        var result = await delegateResolver.ResolveAsync(request, CancellationToken.None);

        // Assert
        result.Should().Be(null);
    }

    [Fact]
    public async Task ResolveAsync_WithAsyncDelegate_HandlesAsyncCorrectly()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var delegateResolver = new DelegateResourceResolver<TestRequest, TestDocument>(
            async (request, sp, ct) =>
            {
                await Task.Delay(1, ct);
                return new TestDocument(request.DocumentId, "Async Content");
            },
            serviceProvider);

        var request = new TestRequest { DocumentId = Guid.NewGuid() };

        // Act
        var result = await delegateResolver.ResolveAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Content.Should().Be("Async Content");
    }

    public interface ITestService
    {
        string GetContent();
    }

    public class TestService : ITestService
    {
        public string GetContent() => "Service Content";
    }
}
